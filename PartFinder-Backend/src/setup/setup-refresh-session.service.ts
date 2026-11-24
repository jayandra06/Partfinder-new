import { Injectable, UnauthorizedException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { JwtService } from '@nestjs/jwt';
import * as bcrypt from 'bcrypt';
import { randomBytes } from 'crypto';
import { Model } from 'mongoose';
import {
  SetupRefreshSession,
  SetupRefreshSessionDocument,
} from './schemas/setup-refresh-session.schema';

const REFRESH_TTL_DAYS = 90;
const BCRYPT_ROUNDS = 10;

export type RefreshResult = {
  accessToken: string;
  refreshToken: string;
  email: string;
};

@Injectable()
export class SetupRefreshSessionService {
  constructor(
    @InjectModel(SetupRefreshSession.name)
    private readonly sessions: Model<SetupRefreshSessionDocument>,
    private readonly jwtService: JwtService,
  ) {}

  private async issueAccessToken(email: string, orgCode: string): Promise<string> {
    const normalized = email.trim().toLowerCase();
    return this.jwtService.signAsync(
      { sub: normalized, email: normalized, orgCode: orgCode.trim() },
      { expiresIn: '1h' },
    );
  }

  private newRefreshTokenPlain(): string {
    return randomBytes(48).toString('base64url');
  }

  async createSession(
    orgCode: string,
    email: string,
    deviceId: string,
  ): Promise<{ refreshToken: string; accessToken: string }> {
    const code = orgCode.trim();
    const normalized = email.trim().toLowerCase();
    const device = deviceId.trim();
    if (!device) {
      throw new UnauthorizedException('Device identifier is required.');
    }

    const refreshToken = this.newRefreshTokenPlain();
    const refreshTokenHash = await bcrypt.hash(refreshToken, BCRYPT_ROUNDS);
    const sessionId = randomBytes(16).toString('hex');
    const expiresAt = new Date(
      Date.now() + REFRESH_TTL_DAYS * 24 * 60 * 60 * 1000,
    );

    await this.sessions.findOneAndUpdate(
      { orgCode: code, emailNormalized: normalized, deviceId: device },
      {
        $set: {
          orgCode: code,
          emailNormalized: normalized,
          deviceId: device,
          sessionId,
          refreshTokenHash,
          previousRefreshTokenHash: null,
          isActive: true,
          expiresAt,
        },
      },
      { upsert: true, new: true },
    );

    const accessToken = await this.issueAccessToken(normalized, code);
    return { refreshToken, accessToken };
  }

  async refreshSession(
    orgCode: string,
    email: string,
    deviceId: string,
    refreshToken: string,
  ): Promise<RefreshResult> {
    const code = orgCode.trim();
    const normalized = email.trim().toLowerCase();
    const device = deviceId.trim();
    const doc = await this.sessions.findOne({
      orgCode: code,
      emailNormalized: normalized,
      deviceId: device,
      isActive: true,
    });

    if (!doc || doc.expiresAt.getTime() < Date.now()) {
      throw new UnauthorizedException('Session expired or invalid.');
    }

    const matchesCurrent = await bcrypt.compare(
      refreshToken,
      doc.refreshTokenHash,
    );
    if (matchesCurrent) {
      const newRefresh = this.newRefreshTokenPlain();
      const newHash = await bcrypt.hash(newRefresh, BCRYPT_ROUNDS);
      doc.previousRefreshTokenHash = doc.refreshTokenHash;
      doc.refreshTokenHash = newHash;
      doc.expiresAt = new Date(
        Date.now() + REFRESH_TTL_DAYS * 24 * 60 * 60 * 1000,
      );
      await doc.save();

      const accessToken = await this.issueAccessToken(normalized, code);
      return {
        accessToken,
        refreshToken: newRefresh,
        email: normalized,
      };
    }

    const prev = doc.previousRefreshTokenHash?.trim();
    if (prev) {
      const matchesPrevious = await bcrypt.compare(refreshToken, prev);
      if (matchesPrevious) {
        await this.sessions.updateMany(
          { orgCode: code, emailNormalized: normalized, deviceId: device },
          { $set: { isActive: false } },
        );
        throw new UnauthorizedException('Refresh token reuse detected.');
      }
    }

    throw new UnauthorizedException('Session expired or invalid.');
  }

  async revokeSession(
    orgCode: string,
    email: string,
    deviceId: string,
    refreshToken?: string,
  ): Promise<void> {
    const code = orgCode.trim();
    const normalized = email.trim().toLowerCase();
    const device = deviceId.trim();
    const doc = await this.sessions.findOne({
      orgCode: code,
      emailNormalized: normalized,
      deviceId: device,
    });
    if (!doc) {
      return;
    }

    if (refreshToken) {
      const ok =
        (await bcrypt.compare(refreshToken, doc.refreshTokenHash)) ||
        (doc.previousRefreshTokenHash
          ? await bcrypt.compare(refreshToken, doc.previousRefreshTokenHash)
          : false);
      if (!ok) {
        return;
      }
    }

    doc.isActive = false;
    await doc.save();
  }

  async revokeAllForUser(orgCode: string, email: string): Promise<void> {
    const code = orgCode.trim();
    const normalized = email.trim().toLowerCase();
    await this.sessions.updateMany(
      { orgCode: code, emailNormalized: normalized },
      { $set: { isActive: false } },
    );
  }
}