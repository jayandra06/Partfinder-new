import {
  BadRequestException,
  ConflictException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { InjectModel } from '@nestjs/mongoose';
import * as nodemailer from 'nodemailer';
import { pbkdf2Sync, randomBytes, timingSafeEqual } from 'crypto';
import { Model, Types } from 'mongoose';
import { BanLicenseDto } from './dto/ban-license.dto';
import { CreateOrganizationDto } from './dto/create-organization.dto';
import { UpdateOrganizationDto } from './dto/update-organization.dto';
import { computeValidity } from './org.constants';
import { PlatformSettingsService } from '../platform/platform-settings.service';
import { Organization, OrganizationDocument } from './schemas/organization.schema';

@Injectable()
export class OrganizationsService {
  constructor(
    @InjectModel(Organization.name)
    private readonly orgModel: Model<OrganizationDocument>,
    private readonly platformSettings: PlatformSettingsService,
    private readonly config: ConfigService,
  ) {}

  async findAll() {
    const list = await this.orgModel.find().sort({ createdAt: -1 }).exec();
    return list.map((doc) => {
      const row = doc.toJSON() as unknown as Record<string, unknown> & {
        _id: { toString(): string };
      };
      const permBanned = Boolean(row.licensePermanentlyBanned);
      const untilRaw = row.licenseBannedUntil as Date | string | null | undefined;
      const until =
        untilRaw == null
          ? null
          : untilRaw instanceof Date
            ? untilRaw
            : new Date(untilRaw);
      return {
        id: row._id.toString(),
        orgCode: String(row.orgCode),
        name: String(row.name),
        type: String(row.type),
        plan: String(row.plan),
        validity: row.validity as Date,
        status: String(row.status),
        licensePermanentlyBanned: permBanned,
        licenseBannedUntil:
          until && !Number.isNaN(until.getTime()) ? until.toISOString() : null,
        orgDatabaseUri: row.orgDatabaseUri != null ? String(row.orgDatabaseUri) : null,
        maxUsers: typeof row.maxUsers === 'number' ? row.maxUsers : 50,
        maxParts: typeof row.maxParts === 'number' ? row.maxParts : 100000,
        createdAt: row.createdAt as Date,
      };
    });
  }

  /**
   * Public license check for desktop clients. Organization code acts as license key;
   * subscription must be Active and validity (UTC end-of-day) must not be past.
   */
  async verifyLicense(rawCode: string) {
    const maintenance = await this.platformSettings.getEffectiveMaintenance();
    if (maintenance.active && maintenance.maintenanceUntil) {
      return {
        valid: false as const,
        reason: 'PLATFORM_MAINTENANCE' as const,
        message:
          'PartFinder is under scheduled maintenance. The Windows app will be available again when the maintenance window ends.',
        maintenanceUntil: maintenance.maintenanceUntil,
      };
    }

    const orgCode = rawCode.trim();
    if (!/^\d{6}$/.test(orgCode)) {
      return {
        valid: false as const,
        reason: 'INVALID_FORMAT' as const,
        message: 'Organization code must be exactly 6 digits.',
      };
    }

    const org = await this.orgModel.findOne({ orgCode }).exec();
    if (!org) {
      return {
        valid: false as const,
        reason: 'UNKNOWN_ORG' as const,
        message: 'Organization code was not found.',
      };
    }

    const status = (org.status ?? '').trim().toLowerCase();
    if (status !== 'active') {
      return {
        valid: false as const,
        reason: 'SUSPENDED' as const,
        message: 'This organization is not active. Contact your administrator.',
        organizationName: org.name,
        orgCode: org.orgCode,
      };
    }

    if (org.licensePermanentlyBanned === true) {
      return {
        valid: false as const,
        reason: 'LICENSE_BANNED_PERMANENT' as const,
        message:
          'This license has been permanently revoked. Contact your administrator if you believe this is a mistake.',
        organizationName: org.name,
        orgCode: org.orgCode,
      };
    }

    const bannedUntilRaw = org.licenseBannedUntil;
    if (bannedUntilRaw != null) {
      const bannedUntil =
        bannedUntilRaw instanceof Date
          ? bannedUntilRaw
          : new Date(bannedUntilRaw);
      if (!Number.isNaN(bannedUntil.getTime()) && Date.now() < bannedUntil.getTime()) {
        return {
          valid: false as const,
          reason: 'LICENSE_BANNED_TEMPORARY' as const,
          message: `This license is temporarily suspended until ${bannedUntil.toISOString()}. Contact your administrator.`,
          organizationName: org.name,
          orgCode: org.orgCode,
          bannedUntil: bannedUntil.toISOString(),
        };
      }
    }

    const validity =
      org.validity instanceof Date ? org.validity : new Date(org.validity);
    const endOfValidityUtc = new Date(validity);
    endOfValidityUtc.setUTCHours(23, 59, 59, 999);

    if (Date.now() > endOfValidityUtc.getTime()) {
      return {
        valid: false as const,
        reason: 'SUBSCRIPTION_EXPIRED' as const,
        message:
          'Your organization subscription has expired. You cannot use PartFinder until it is renewed.',
        organizationName: org.name,
        orgCode: org.orgCode,
        validUntil: validity.toISOString(),
      };
    }

    return {
      valid: true as const,
      reason: null,
      message: null,
      organizationName: org.name,
      orgCode: org.orgCode,
      validUntil: validity.toISOString(),
      maxUsers: org.maxUsers ?? 50,
      maxParts: org.maxParts ?? 100000,
    };
  }

  async create(dto: CreateOrganizationDto) {
    const existing = await this.orgModel.findOne({ orgCode: dto.orgCode }).exec();
    if (existing) {
      throw new ConflictException('Organization code already exists');
    }
    const firstAdminEmail = dto.firstAdminEmail.trim().toLowerCase();
    const temporaryPassword = this.generateTemporaryPassword();
    const created = new this.orgModel({
      orgCode: dto.orgCode,
      name: dto.name.trim(),
      type: dto.type,
      plan: dto.plan,
      validity: computeValidity(dto.plan),
      status: 'Active',
      licensePermanentlyBanned: false,
      licenseBannedUntil: null,
      orgDatabaseUri: null,
      maxUsers: 50,
      maxParts: 100000,
      firstAdminEmail,
      firstAdminEmailNormalized: firstAdminEmail,
      firstAdminTemporaryPasswordHash:
        this.hashTemporaryPassword(temporaryPassword),
      firstAdminInviteSentAtUtc: new Date(),
    });
    await created.save();
    await this.sendFirstAdminInviteEmail({
      email: firstAdminEmail,
      orgCode: dto.orgCode.trim(),
      temporaryPassword,
    });
    return this.findAll();
  }

  async update(id: string, dto: UpdateOrganizationDto) {
    if (!Types.ObjectId.isValid(id)) {
      throw new BadRequestException('Invalid organization id');
    }
    const org = await this.orgModel.findById(id).exec();
    if (!org) {
      throw new NotFoundException('Organization not found');
    }
    if (dto.name !== undefined) {
      org.name = dto.name.trim();
    }
    if (dto.type !== undefined) {
      org.type = dto.type;
    }
    if (dto.status !== undefined) {
      org.status = dto.status;
    }
    if (dto.plan !== undefined) {
      org.plan = dto.plan;
      org.validity = computeValidity(dto.plan);
    }
    await org.save();
    return this.findAll();
  }

  async banLicense(id: string, dto: BanLicenseDto) {
    if (!Types.ObjectId.isValid(id)) {
      throw new BadRequestException('Invalid organization id');
    }
    const org = await this.orgModel.findById(id).exec();
    if (!org) {
      throw new NotFoundException('Organization not found');
    }
    const permanent = dto.permanent === true;
    const minutes = dto.minutes ?? 0;
    const hours = dto.hours ?? 0;
    const days = dto.days ?? 0;
    if (!permanent) {
      const totalMs = ((days * 24 + hours) * 60 + minutes) * 60 * 1000;
      if (totalMs <= 0) {
        throw new BadRequestException(
          'Set permanent to true, or enter a positive duration using minutes, hours, and/or days.',
        );
      }
      org.licensePermanentlyBanned = false;
      org.licenseBannedUntil = new Date(Date.now() + totalMs);
    } else {
      org.licensePermanentlyBanned = true;
      org.licenseBannedUntil = null;
    }
    await org.save();
    return this.findAll();
  }

  async reactivateLicense(id: string) {
    if (!Types.ObjectId.isValid(id)) {
      throw new BadRequestException('Invalid organization id');
    }
    const org = await this.orgModel.findById(id).exec();
    if (!org) {
      throw new NotFoundException('Organization not found');
    }
    org.licensePermanentlyBanned = false;
    org.licenseBannedUntil = null;
    await org.save();
    return this.findAll();
  }

  async findByOrgCode(orgCode: string): Promise<OrganizationDocument | null> {
    return this.orgModel.findOne({ orgCode: orgCode.trim() }).exec();
  }

  private generateTemporaryPassword() {
    const alphabet =
      'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%';
    const bytes = randomBytes(12);
    let out = '';
    for (let i = 0; i < bytes.length; i++) {
      out += alphabet[bytes[i] % alphabet.length];
    }
    return out;
  }

  private hashTemporaryPassword(password: string) {
    const iterations = 120000;
    const salt = randomBytes(16);
    const hash = pbkdf2Sync(password, salt, iterations, 32, 'sha256');
    return `pbkdf2$${iterations}$${salt.toString('base64')}$${hash.toString('base64')}`;
  }

  verifyTemporaryPassword(password: string, stored: string) {
    if (!stored) {
      return false;
    }
    const parts = stored.split('$');
    if (parts.length !== 4 || parts[0] !== 'pbkdf2') {
      return false;
    }
    const iterations = Number(parts[1]);
    if (!Number.isFinite(iterations) || iterations < 10000) {
      return false;
    }
    try {
      const salt = Buffer.from(parts[2], 'base64');
      const expected = Buffer.from(parts[3], 'base64');
      const actual = pbkdf2Sync(password, salt, iterations, expected.length, 'sha256');
      return timingSafeEqual(actual, expected);
    } catch {
      return false;
    }
  }

  private async sendFirstAdminInviteEmail(input: {
    email: string;
    orgCode: string;
    temporaryPassword: string;
  }) {
    const host = this.config.get<string>('INVITE_SMTP_HOST')?.trim() ?? '';
    const port = Number(this.config.get<string>('INVITE_SMTP_PORT') ?? '587');
    const user = this.config.get<string>('INVITE_SMTP_USER')?.trim() ?? '';
    const pass = this.config.get<string>('INVITE_SMTP_PASS') ?? '';
    const from = this.config.get<string>('INVITE_FROM_EMAIL')?.trim() ?? '';
    const fromName =
      this.config.get<string>('INVITE_FROM_NAME')?.trim() || 'PartFinder';
    const downloadLink =
      this.config.get<string>('INVITE_DOWNLOAD_LINK')?.trim() ||
      'https://shipspan.com';
    const secure =
      (this.config.get<string>('INVITE_SMTP_SECURE') ?? 'false')
        .toLowerCase()
        .trim() === 'true';

    if (!host || !user || !pass || !from) {
      return;
    }

    const transporter = nodemailer.createTransport({
      host,
      port: Number.isFinite(port) && port > 0 ? port : 587,
      secure,
      auth: { user, pass },
    });

    const subject = `PartFinder admin invite - Org ${input.orgCode}`;
    const text = [
      'You have been invited as the first organization admin for PartFinder.',
      '',
      `Organization Code: ${input.orgCode}`,
      `Email: ${input.email}`,
      `Temporary Password: ${input.temporaryPassword}`,
      '',
      'Windows app download:',
      downloadLink,
      '',
      'Setup flow:',
      '1) Enter org code, invited email, and temporary password in Step 1.',
      '2) Configure database in Step 2.',
      '3) Change temporary password in Step 3.',
    ].join('\n');

    try {
      await transporter.sendMail({
        from: `"${fromName}" <${from}>`,
        to: input.email,
        subject,
        text,
      });
    } catch {
      // Keep organization creation successful even if SMTP fails.
    }
  }

  async remove(id: string) {
    if (!Types.ObjectId.isValid(id)) {
      throw new BadRequestException('Invalid organization id');
    }
    const deleted = await this.orgModel.findByIdAndDelete(id).exec();
    if (!deleted) {
      throw new NotFoundException('Organization not found');
    }
    return this.findAll();
  }
}