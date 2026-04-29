import {
  BadRequestException,
  ConflictException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as bcrypt from 'bcrypt';
import { pbkdf2Sync, randomBytes, timingSafeEqual } from 'crypto';
import * as nodemailer from 'nodemailer';
import { DbClustersService } from '../db-clusters/db-clusters.service';
import {
  assertMongoUri,
  prepareMongoUriForDriver,
} from '../mongo-uri';
import { Organization } from '../organizations/schemas/organization.schema';
import { OrganizationsService } from '../organizations/organizations.service';
import { SetupCustomDatabaseDto } from './dto/setup-custom-database.dto';
import { SetupInviteLoginDto } from './dto/setup-invite-login.dto';
import { SetupInviteUserDto } from './dto/setup-invite-user.dto';
import { SetupOrgAdminDto } from './dto/setup-org-admin.dto';
import { replaceMongoDatabasePath } from './org-database-uri.util';
import { TenantMongoService } from './tenant-mongo.service';

const BCRYPT_ROUNDS = 10;

@Injectable()
export class SetupService {
  constructor(
    private readonly orgs: OrganizationsService,
    private readonly tenant: TenantMongoService,
    private readonly dbClusters: DbClustersService,
    private readonly config: ConfigService,
  ) {}

  private async loadLicensedOrg(orgCode: string) {
    const v = await this.orgs.verifyLicense(orgCode);
    if (!v.valid) {
      throw new BadRequestException(v.message ?? 'License validation failed');
    }
    const org = await this.orgs.findByOrgCode(orgCode);
    if (!org) {
      throw new NotFoundException('Organization not found');
    }
    return { verify: v, org };
  }

  async status(orgCode: string) {
    const v = await this.orgs.verifyLicense(orgCode);
    if (!v.valid) {
      const maintenanceUntil =
        'maintenanceUntil' in v && typeof v.maintenanceUntil === 'string'
          ? v.maintenanceUntil
          : undefined;
      return {
        valid: false as const,
        reason: v.reason,
        message: v.message,
        ...(maintenanceUntil ? { maintenanceUntil } : {}),
      };
    }
    const org = await this.orgs.findByOrgCode(orgCode);
    if (!org) {
      return {
        valid: false as const,
        reason: 'UNKNOWN_ORG' as const,
        message: 'Organization code was not found.',
      };
    }
    const uri = org.orgDatabaseUri?.trim() || null;
    const hasOrgDatabase = Boolean(uri);
    let orgAdminStatus: 'yes' | 'no' | 'unknown' = 'no';
    let serverReachedDatabase: boolean | null = null;

    if (hasOrgDatabase && uri) {
      try {
        await this.tenant.pingUri(uri);
        serverReachedDatabase = true;
        const has = await this.tenant.hasAnyOrgAdmin(uri);
        orgAdminStatus = has ? 'yes' : 'no';
      } catch {
        serverReachedDatabase = false;
        orgAdminStatus = 'unknown';
      }
    }

    const validity =
      org.validity instanceof Date ? org.validity : new Date(org.validity);

    return {
      valid: true as const,
      organizationName: org.name,
      orgCode: org.orgCode,
      status: org.status,
      plan: org.plan,
      orgType: org.type,
      validUntil: validity.toISOString(),
      maxUsers: org.maxUsers ?? 50,
      maxParts: org.maxParts ?? 100000,
      hasOrgDatabase,
      orgDatabaseUri: hasOrgDatabase ? uri : null,
      orgAdminStatus,
      serverReachedDatabase,
      requiresInviteLogin: Boolean(
        org.firstAdminEmailNormalized && org.firstAdminTemporaryPasswordHash,
      ),
      firstAdminEmail: org.firstAdminEmailNormalized ?? null,
    };
  }

  async provisionDefaultDatabase(orgCode: string) {
    const { org } = await this.loadLicensedOrg(orgCode);
    if (org.orgDatabaseUri?.trim()) {
      throw new ConflictException('Organization database is already configured');
    }
    const code = orgCode.trim();
    if (!/^\d{6}$/.test(code)) {
      throw new BadRequestException(
        'Organization code must be exactly 6 digits for default database provisioning.',
      );
    }
    const picked = await this.dbClusters.pickClusterForProvisioning();
    if (!picked) {
      throw new BadRequestException(
        'No database cluster has capacity. Add a cluster in Admin → Database Management, or use a custom connection string.',
      );
    }
    const base = prepareMongoUriForDriver(picked.connectionUri);
    try {
      assertMongoUri(base);
    } catch {
      throw new BadRequestException('Registered cluster URI is invalid.');
    }
    const uri = replaceMongoDatabasePath(base, code);
    await this.tenant.pingUri(uri);
    await this.tenant.initializeTenantDatabase(uri);
    org.orgDatabaseUri = uri;
    org.assignedDbClusterId =
      picked.clusterId as unknown as Organization['assignedDbClusterId'];
    await org.save();
    return { ok: true as const, orgDatabaseUri: uri };
  }

  async saveCustomDatabase(dto: SetupCustomDatabaseDto) {
    const { org } = await this.loadLicensedOrg(dto.orgCode);
    if (org.orgDatabaseUri?.trim()) {
      throw new ConflictException('Organization database is already configured');
    }
    const uri = dto.uri.trim();
    try {
      assertMongoUri(uri);
    } catch {
      throw new BadRequestException('Invalid MongoDB URI');
    }
    if (dto.clientInitializationConfirmed === true) {
      org.orgDatabaseUri = uri;
      org.assignedDbClusterId = null;
      await org.save();
      return { ok: true as const, orgDatabaseUri: uri, clientOnlyInit: true };
    }
    await this.tenant.pingUri(uri);
    await this.tenant.initializeTenantDatabase(uri);
    org.orgDatabaseUri = uri;
    org.assignedDbClusterId = null;
    await org.save();
    return { ok: true as const, orgDatabaseUri: uri, clientOnlyInit: false };
  }

  async testDatabase(orgCode: string, uriOverride?: string) {
    const { org } = await this.loadLicensedOrg(orgCode);
    const uri = (uriOverride?.trim() || org.orgDatabaseUri?.trim()) ?? '';
    if (!uri) {
      throw new BadRequestException('No database URI to test');
    }
    assertMongoUri(uri);
    await this.tenant.pingUri(uri);
    return { ok: true as const };
  }

  async createOrgAdmin(dto: SetupOrgAdminDto) {
    await this.loadLicensedOrg(dto.orgCode);
    const org = await this.orgs.findByOrgCode(dto.orgCode);
    if (!org?.orgDatabaseUri?.trim()) {
      throw new BadRequestException(
        'Organization database is not configured yet.',
      );
    }
    const uri = org.orgDatabaseUri.trim();
    const firstAdminEmail = (org.firstAdminEmailNormalized ?? '').trim();
    const firstAdminTempHash = (org.firstAdminTemporaryPasswordHash ?? '').trim();
    if (!firstAdminEmail || !firstAdminTempHash) {
      throw new BadRequestException(
        'First admin invite is not pending for this organization.',
      );
    }
    if (!firstAdminEmail || firstAdminEmail !== dto.email.trim().toLowerCase()) {
      throw new BadRequestException('Email does not match invited first admin.');
    }
    if (!this.orgs.verifyTemporaryPassword(dto.oldPassword, firstAdminTempHash)) {
      throw new BadRequestException('Current temporary password is invalid.');
    }
    const passwordHash = await bcrypt.hash(dto.newPassword, BCRYPT_ROUNDS);
    const adminName = org.name?.trim() ? `${org.name.trim()} Admin` : 'Organization Admin';
    const result = await this.tenant.createOrgAdminIfNone(
      uri,
      adminName,
      dto.email,
      passwordHash,
    );
    if (result.created || result.skipped) {
      org.firstAdminTemporaryPasswordHash = null;
      await org.save();
    }
    return {
      ok: true as const,
      created: result.created,
      skipped: result.skipped,
    };
  }

  async inviteOrgUser(dto: SetupInviteUserDto) {
    await this.loadLicensedOrg(dto.orgCode);
    const org = await this.orgs.findByOrgCode(dto.orgCode);
    if (!org?.orgDatabaseUri?.trim()) {
      throw new BadRequestException(
        'Organization database is not configured yet.',
      );
    }

    const uri = org.orgDatabaseUri.trim();
    const email = dto.email.trim().toLowerCase();
    const existing = await this.tenant.findOrgAppUserByEmail(uri, email);
    if (existing) {
      throw new ConflictException('A user with this email is already invited.');
    }

    const role = dto.role;
    const partsAllTemplates =
      role === 'Admin' ? true : dto.partsAllTemplates === true;
    const allowedTemplateIds = (dto.allowedTemplateIds ?? [])
      .map((x: string) => x.trim())
      .filter(Boolean);
    const temporaryPassword = this.generateTemporaryPassword();

    await this.tenant.insertOrgAppUser(uri, {
      name: dto.name.trim(),
      email,
      emailNormalized: email,
      role,
      partsAllTemplates,
      allowedTemplateIds,
      temporaryPasswordHash: this.hashTemporaryPassword(temporaryPassword),
      invitedAtUtc: new Date(),
    });

    const emailResult = await this.sendInviteEmail(
      email,
      dto.name.trim(),
      dto.orgCode.trim(),
      role,
      temporaryPassword,
    );

    return {
      ok: true as const,
      email,
      organizationCode: dto.orgCode.trim(),
      temporaryPassword,
      emailSent: emailResult.sent,
      emailError: emailResult.error ?? null,
    };
  }

  async validateInviteLogin(dto: SetupInviteLoginDto) {
    await this.loadLicensedOrg(dto.orgCode);
    const org = await this.orgs.findByOrgCode(dto.orgCode);
    if (!org) {
      return { ok: false as const, message: 'Organization not found.' };
    }

    const firstAdminEmail = (org.firstAdminEmailNormalized ?? '').trim();
    const firstAdminTempHash = (org.firstAdminTemporaryPasswordHash ?? '').trim();
    const reqEmail = dto.email.trim().toLowerCase();
    if (
      firstAdminEmail &&
      firstAdminTempHash &&
      reqEmail === firstAdminEmail &&
      this.orgs.verifyTemporaryPassword(dto.temporaryPassword, firstAdminTempHash)
    ) {
      return {
        ok: true as const,
        email: reqEmail,
        role: 'Admin',
      };
    }

    if (!org.orgDatabaseUri?.trim()) {
      return { ok: false as const, message: 'Invalid invited credentials.' };
    }
    const uri = org.orgDatabaseUri.trim();

    const orgAdmin = await this.tenant.findOrgAdminByEmail(uri, dto.email);
    if (
      orgAdmin &&
      typeof orgAdmin.passwordHash === 'string' &&
      orgAdmin.passwordHash &&
      (await bcrypt.compare(dto.temporaryPassword, orgAdmin.passwordHash))
    ) {
      return {
        ok: true as const,
        email: dto.email.trim().toLowerCase(),
        role: 'Admin',
      };
    }

    const doc = await this.tenant.findOrgAppUserByEmail(uri, dto.email);
    if (!doc) {
      return { ok: false as const, message: 'Invalid invited credentials.' };
    }

    const hash =
      typeof doc.temporaryPasswordHash === 'string'
        ? doc.temporaryPasswordHash
        : '';
    const valid = this.verifyTemporaryPassword(dto.temporaryPassword, hash);
    if (!valid) {
      return { ok: false as const, message: 'Invalid invited credentials.' };
    }

    return {
      ok: true as const,
      email: dto.email.trim().toLowerCase(),
      role: typeof doc.role === 'string' ? doc.role : 'Employee',
    };
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

  private verifyTemporaryPassword(password: string, stored: string) {
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

    let salt: Buffer;
    let expected: Buffer;
    try {
      salt = Buffer.from(parts[2], 'base64');
      expected = Buffer.from(parts[3], 'base64');
    } catch {
      return false;
    }

    const actual = pbkdf2Sync(password, salt, iterations, expected.length, 'sha256');
    return timingSafeEqual(actual, expected);
  }

  private async sendInviteEmail(
    email: string,
    name: string,
    orgCode: string,
    role: string,
    temporaryPassword: string,
  ) {
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
      return { sent: false, error: 'SMTP invite configuration is missing.' };
    }

    try {
      const transporter = nodemailer.createTransport({
        host,
        port: Number.isFinite(port) && port > 0 ? port : 587,
        secure,
        auth: { user, pass },
      });

      await transporter.sendMail({
        from: `"${fromName}" <${from}>`,
        to: email,
        subject: 'PartFinder Invite',
        text:
          `Hello ${name},\n\n` +
          `You have been invited to PartFinder.\n` +
          `Organization code: ${orgCode}\n` +
          `Email: ${email}\n` +
          `Temporary password: ${temporaryPassword}\n` +
          `Role: ${role}\n\n` +
          `Download app: ${downloadLink}\n\n` +
          `After installing, enter your organization code, then sign in with this email and temporary password.`,
      });

      return { sent: true, error: null as string | null };
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Failed to send invite email.';
      return { sent: false, error: msg };
    }
  }
}
