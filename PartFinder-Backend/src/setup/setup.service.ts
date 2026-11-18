import {
  BadRequestException,
  ConflictException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { OnEvent } from '@nestjs/event-emitter';
import { ConfigService } from '@nestjs/config';
import * as bcrypt from 'bcrypt';
import { pbkdf2Sync, randomBytes } from 'crypto';
import * as nodemailer from 'nodemailer';
import { DbClustersService } from '../db-clusters/db-clusters.service';
import {
  assertMongoUri,
  prepareMongoUriForDriver,
} from '../mongo-uri';
import {
  Organization,
  OrganizationDocument,
} from '../organizations/schemas/organization.schema';
import { OrganizationsService } from '../organizations/organizations.service';
import { SetupCustomDatabaseDto } from './dto/setup-custom-database.dto';
import { SetupInviteLoginDto } from './dto/setup-invite-login.dto';
import { SetupInviteUserDto } from './dto/setup-invite-user.dto';
import { SetupOrgAdminDto } from './dto/setup-org-admin.dto';
import { SetupChangePasswordDto } from './dto/setup-change-password.dto';
import { SetupForgotPasswordDto } from './dto/setup-forgot-password.dto';
import { SetupResetPasswordDto } from './dto/setup-reset-password.dto';
import { replaceMongoDatabasePath } from './org-database-uri.util';
import { resolveInviteDownloadLink } from '../common/invite-download-link.util';
import { buildPasswordResetLink } from '../common/password-reset-link.util';
import { TenantMongoService } from './tenant-mongo.service';
import { CasbinService } from '../casbin/casbin.service';
import { JwtService } from '@nestjs/jwt';
import { SetupRefreshSessionService } from './setup-refresh-session.service';
import {
  SetupRefreshSessionDto,
  SetupRevokeSessionDto,
} from './dto/setup-refresh-session.dto';

const BCRYPT_ROUNDS = 10;

@Injectable()
export class SetupService {
  constructor(
    private readonly orgs: OrganizationsService,
    private readonly tenant: TenantMongoService,
    private readonly dbClusters: DbClustersService,
    private readonly config: ConfigService,
    private readonly casbin: CasbinService,
    private readonly jwtService: JwtService,
    private readonly refreshSessions: SetupRefreshSessionService,
  ) {}

  private async issueInviteAccessToken(
    email: string,
    orgCode: string,
  ): Promise<string> {
    const normalized = email.trim().toLowerCase();
    return this.jwtService.signAsync(
      { sub: normalized, email: normalized, orgCode: orgCode.trim() },
      { expiresIn: '1h' },
    );
  }

  private async finalizeInviteLoginSuccess(
    dto: SetupInviteLoginDto,
    email: string,
    role: string,
    mustChangePassword: boolean,
  ) {
    const deviceId = dto.deviceId?.trim() ?? '';
    if (deviceId.length >= 8) {
      const session = await this.refreshSessions.createSession(
        dto.orgCode,
        email,
        deviceId,
      );
      return {
        ok: true as const,
        email,
        role,
        mustChangePassword,
        accessToken: session.accessToken,
        refreshToken: session.refreshToken,
      };
    }

    return {
      ok: true as const,
      email,
      role,
      mustChangePassword,
      accessToken: await this.issueInviteAccessToken(email, dto.orgCode),
    };
  }

  async refreshDeviceSession(dto: SetupRefreshSessionDto) {
    await this.loadLicensedOrg(dto.orgCode);
    const result = await this.refreshSessions.refreshSession(
      dto.orgCode,
      dto.email,
      dto.deviceId,
      dto.refreshToken,
    );
    return { ok: true as const, ...result };
  }

  async revokeDeviceSession(dto: SetupRevokeSessionDto) {
    await this.loadLicensedOrg(dto.orgCode);
    await this.refreshSessions.revokeSession(
      dto.orgCode,
      dto.email,
      dto.deviceId,
      dto.refreshToken,
    );
    return { ok: true as const };
  }

  @OnEvent('admin.password.changed')
  async handleAdminPasswordChanged(payload: { email: string; targetEmail?: string; plainPassword: string }) {
    console.log(`[handleAdminPasswordChanged] Received event for email: ${payload.email}, targetEmail: ${payload.targetEmail}`);
    try {
      const orgs = await this.orgs.findAll();
      console.log(`[handleAdminPasswordChanged] Found ${orgs.length} organizations in total.`);
      const emailLower = payload.email.toLowerCase().trim();
      const targetEmailLower = payload.targetEmail ? payload.targetEmail.toLowerCase().trim() : '';
      const emailsToUpdate = [emailLower];
      if (targetEmailLower && targetEmailLower !== emailLower) {
        emailsToUpdate.push(targetEmailLower);
      }

      const passwordHash = await bcrypt.hash(payload.plainPassword, BCRYPT_ROUNDS);
      const pbkdf2HashString = this.hashTemporaryPassword(payload.plainPassword);

      for (const org of orgs) {
        if (!org.orgDatabaseUri) {
          console.log(`[handleAdminPasswordChanged] Org ${org.orgCode} has no database URI. Skipping.`);
          continue;
        }
        try {
          console.log(`[handleAdminPasswordChanged] Syncing to Org: ${org.orgCode} with DB URI: ${org.orgDatabaseUri}`);
          const conn = await this.tenant.getConnection(org.orgDatabaseUri);

          for (const emailToUpdate of emailsToUpdate) {
            // Update OrgAdmin
            const adminColl = this.tenant.orgAdminCollection(conn);
            const adminResult = await adminColl.updateOne(
              { email: emailToUpdate },
              { $set: { passwordHash } }
            );
            console.log(`[handleAdminPasswordChanged] OrgAdmin update for ${org.orgCode} (${emailToUpdate}): matchedCount = ${adminResult.matchedCount}, modifiedCount = ${adminResult.modifiedCount}`);

            // Update OrgAppUser
            const userColl = this.tenant.orgAppUsersCollection(conn);
            const userResult = await userColl.updateOne(
              { emailNormalized: emailToUpdate },
              { $set: { temporaryPasswordHash: pbkdf2HashString } }
            );
            console.log(`[handleAdminPasswordChanged] OrgAppUser update for ${org.orgCode} (${emailToUpdate}): matchedCount = ${userResult.matchedCount}, modifiedCount = ${userResult.modifiedCount}`);
          }
        } catch (e) {
          console.error(`[handleAdminPasswordChanged] Failed to sync password for tenant ${org.orgCode}`, e);
        }
      }
    } catch (err) {
      console.error(`[handleAdminPasswordChanged] Critical error in handler:`, err);
    }
  }

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

    let orgAppUsers: Array<{
      id: string;
      name: string;
      email: string;
      role: string;
      partsAllTemplates: boolean;
      allowedTemplateIds: string[];
      invitedAtUtc: string | null;
      templatePermissions: unknown;
      masterDataPermissions: unknown;
      status: string;
    }> = [];

    if (hasOrgDatabase && uri) {
      try {
        await this.tenant.pingUri(uri);
        serverReachedDatabase = true;
        const has = await this.tenant.hasAnyOrgAdmin(uri);
        orgAdminStatus = has ? 'yes' : 'no';
        orgAppUsers = await this.mapOrgAppUserDocuments(uri);
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
      wmsEnabled: v.wmsEnabled === true,
      maxWmsWarehouses: org.maxWmsWarehouses ?? 10,
      maxStockOwners: org.maxStockOwners ?? 50,
      hasOrgDatabase,
      orgDatabaseUri: hasOrgDatabase ? uri : null,
      orgAdminStatus,
      serverReachedDatabase,
      orgAppUsers,
      requiresInviteLogin: Boolean(
        org.firstAdminEmailNormalized && org.firstAdminTemporaryPasswordHash,
      ),
      firstAdminEmail: org.firstAdminEmailNormalized ?? null,
    };
  }

  private async mapOrgAppUserDocuments(uri: string) {
    const docs = await this.tenant.listOrgAppUsers(uri);
    return docs.map((doc) => {
      const tempHash =
        typeof doc.temporaryPasswordHash === 'string'
          ? doc.temporaryPasswordHash.trim()
          : '';
      const status = tempHash ? 'Pending' : 'Active';
      const invitedRaw = doc.invitedAtUtc;
      let invitedAtUtc: string | null = null;
      if (invitedRaw instanceof Date) {
        invitedAtUtc = invitedRaw.toISOString();
      } else if (invitedRaw) {
        invitedAtUtc = new Date(invitedRaw as string | number).toISOString();
      }

      return {
        id: doc._id?.toString?.() ?? '',
        name: typeof doc.name === 'string' ? doc.name : '',
        email: typeof doc.email === 'string' ? doc.email : '',
        role: typeof doc.role === 'string' ? doc.role : 'Employee',
        partsAllTemplates: doc.partsAllTemplates === true,
        allowedTemplateIds: Array.isArray(doc.allowedTemplateIds)
          ? doc.allowedTemplateIds.filter(
              (x): x is string => typeof x === 'string',
            )
          : [],
        invitedAtUtc,
        templatePermissions: doc.templatePermissions ?? null,
        masterDataPermissions: doc.masterDataPermissions ?? null,
        status,
      };
    });
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
    const email = dto.email.trim().toLowerCase();
    const match = await this.currentPasswordMatches(org, email, dto.oldPassword);
    if (!match) {
      throw new BadRequestException('Current temporary password is invalid.');
    }

    const passwordHash = await bcrypt.hash(dto.newPassword, BCRYPT_ROUNDS);
    const result = await this.storeReplacedPassword(org, uri, email, passwordHash, match);
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
      templatePermissions: dto.templatePermissions,
      masterDataPermissions: dto.masterDataPermissions,
      temporaryPasswordHash: this.hashTemporaryPassword(temporaryPassword),
      invitedAtUtc: new Date(),
    });

    // Sync granular permissions to Casbin
    try {
      await this.casbin.syncUserPermissions(
        uri,
        email,
        role,
        dto.templatePermissions,
        dto.masterDataPermissions
      );
    } catch (e) {
      console.error('Failed to sync Casbin policy:', e);
      // We don't throw here to avoid failing the whole invite, 
      // but in production you might want more robust handling.
    }

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

  async listOrgUsers(orgCode: string) {
    const { org } = await this.loadLicensedOrg(orgCode);
    if (!org?.orgDatabaseUri?.trim()) {
      throw new BadRequestException(
        'Organization database is not configured yet.',
      );
    }

    const uri = org.orgDatabaseUri.trim();
    const conn = await this.tenant.getConnection(uri);
    const appColl = this.tenant.orgAppUsersCollection(conn);
    const adminColl = this.tenant.orgAdminCollection(conn);

    type ListedUser = {
      id: string;
      name: string;
      email: string;
      role: string;
      partsAllTemplates: boolean;
      allowedTemplateIds: string[];
      invitedAtUtc: string;
      templatePermissions: SetupInviteUserDto['templatePermissions'] | null;
      masterDataPermissions: SetupInviteUserDto['masterDataPermissions'] | null;
      status: 'Active' | 'Pending';
      isOrgAdminDirectoryEntry: boolean;
    };

    const appDocs = await appColl.find({}).sort({ invitedAtUtc: -1 }).toArray();
    const appUsers: ListedUser[] = appDocs.map((d) => {
      const email =
        typeof d.email === 'string' && d.email.trim()
          ? d.email.trim()
          : typeof d.emailNormalized === 'string'
            ? d.emailNormalized.trim()
            : '';
      const tempHash =
        typeof d.temporaryPasswordHash === 'string'
          ? d.temporaryPasswordHash.trim()
          : '';
      const permHash =
        typeof d.passwordHash === 'string' ? d.passwordHash.trim() : '';
      const pendingInvite = Boolean(tempHash);
      const hasPermanentPassword = Boolean(permHash);
      const invitedRaw = d.invitedAtUtc;
      const invitedAt =
        invitedRaw instanceof Date
          ? invitedRaw
          : invitedRaw
            ? new Date(invitedRaw as string | number)
            : new Date();
      const allowed = Array.isArray(d.allowedTemplateIds)
        ? (d.allowedTemplateIds as unknown[])
            .filter((x): x is string => typeof x === 'string')
            .map((x) => x.trim())
            .filter(Boolean)
        : [];

      return {
        id: String(d._id),
        name:
          typeof d.name === 'string' && d.name.trim()
            ? d.name.trim()
            : email,
        email,
        role:
          typeof d.role === 'string' && d.role.trim() ? d.role.trim() : 'Employee',
        partsAllTemplates: d.partsAllTemplates === true,
        allowedTemplateIds: allowed,
        invitedAtUtc: invitedAt.toISOString(),
        templatePermissions:
          d.templatePermissions && typeof d.templatePermissions === 'object'
            ? (d.templatePermissions as ListedUser['templatePermissions'])
            : null,
        masterDataPermissions:
          d.masterDataPermissions && typeof d.masterDataPermissions === 'object'
            ? (d.masterDataPermissions as ListedUser['masterDataPermissions'])
            : null,
        status:
          hasPermanentPassword || !pendingInvite ? 'Active' : 'Pending',
        isOrgAdminDirectoryEntry: false,
      };
    });

    const appEmailSet = new Set(
      appUsers.map((u) => u.email.trim().toLowerCase()).filter(Boolean),
    );

    const adminDocs = await adminColl.find({}).toArray();
    const adminUsers: ListedUser[] = [];
    for (const doc of adminDocs) {
      const email =
        typeof doc.email === 'string' ? doc.email.trim().toLowerCase() : '';
      if (!email || appEmailSet.has(email)) {
        continue;
      }

      const name =
        typeof doc.name === 'string' && doc.name.trim()
          ? doc.name.trim()
          : email;
      const permHash =
        typeof doc.passwordHash === 'string' ? doc.passwordHash.trim() : '';
      const createdRaw = doc.createdAt;
      const invitedAt =
        createdRaw instanceof Date
          ? createdRaw
          : createdRaw
            ? new Date(createdRaw as string | number)
            : new Date();

      adminUsers.push({
        id: `org-admin:${email}`,
        name,
        email,
        role: 'Admin',
        partsAllTemplates: true,
        allowedTemplateIds: [],
        invitedAtUtc: invitedAt.toISOString(),
        templatePermissions: null,
        masterDataPermissions: null,
        status: permHash ? 'Active' : 'Pending',
        isOrgAdminDirectoryEntry: true,
      });
    }

    const users = [...adminUsers, ...appUsers].sort(
      (a, b) =>
        new Date(b.invitedAtUtc).getTime() - new Date(a.invitedAtUtc).getTime(),
    );

    return { ok: true as const, orgDatabaseUri: uri, users };
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
      (await this.passwordAccepted(dto.temporaryPassword, firstAdminTempHash, 'pbkdf2'))
    ) {
      return this.finalizeInviteLoginSuccess(dto, reqEmail, 'Admin', true);
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
      (await this.passwordAccepted(dto.temporaryPassword, orgAdmin.passwordHash, 'bcrypt'))
    ) {
      const adminEmail = dto.email.trim().toLowerCase();
      return this.finalizeInviteLoginSuccess(dto, adminEmail, 'Admin', false);
    }

    const doc = await this.tenant.findOrgAppUserByEmail(uri, dto.email);
    if (!doc) {
      return { ok: false as const, message: 'Invalid invited credentials.' };
    }

    const tempHash =
      typeof doc.temporaryPasswordHash === 'string'
        ? doc.temporaryPasswordHash
        : '';

    // If temporaryPasswordHash is empty, user already changed their password.
    // They should now use their permanent password (stored in passwordHash field).
    if (!tempHash) {
      const permanentHash = typeof doc.passwordHash === 'string' ? doc.passwordHash : '';
      if (!permanentHash) {
        return { ok: false as const, message: 'Invalid credentials.' };
      }
      const validPermanent = await this.passwordAccepted(
        dto.temporaryPassword,
        permanentHash,
        'bcrypt',
      );
      if (!validPermanent) {
        return { ok: false as const, message: 'Invalid credentials.' };
      }
      const employeeEmail = dto.email.trim().toLowerCase();
      const employeeRole =
        typeof doc.role === 'string' ? doc.role : 'Employee';
      return this.finalizeInviteLoginSuccess(
        dto,
        employeeEmail,
        employeeRole,
        false,
      );
    }

    // User still has temporary password — verify it
    const valid = await this.passwordAccepted(dto.temporaryPassword, tempHash, 'pbkdf2');
    if (!valid) {
      return { ok: false as const, message: 'Invalid invited credentials.' };
    }

    const pendingEmail = dto.email.trim().toLowerCase();
    const pendingRole = typeof doc.role === 'string' ? doc.role : 'Employee';
    return this.finalizeInviteLoginSuccess(
      dto,
      pendingEmail,
      pendingRole,
      true,
    );
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

  private async sendInviteEmail(
    email: string,
    name: string,
    orgCode: string,
    role: string,
    temporaryPassword: string,
  ) {
    const host =
      this.config.get<string>('INVITE_SMTP_HOST')?.trim() ||
      this.config.get<string>('EMAIL_HOST')?.trim() ||
      '';
    const port = Number(
      this.config.get<string>('INVITE_SMTP_PORT') ??
        this.config.get<string>('EMAIL_PORT') ??
        '587',
    );
    const user =
      this.config.get<string>('INVITE_SMTP_USER')?.trim() ||
      this.config.get<string>('EMAIL_USER')?.trim() ||
      '';
    const pass =
      this.config.get<string>('INVITE_SMTP_PASS') ??
      this.config.get<string>('EMAIL_PASS') ??
      '';
    const from =
      this.config.get<string>('INVITE_FROM_EMAIL')?.trim() ||
      this.config.get<string>('SMTP_FROM')?.trim() ||
      '';
    const fromName =
      this.config.get<string>('INVITE_FROM_NAME')?.trim() || 'PartFinder';
    const downloadLink = resolveInviteDownloadLink(
      this.config.get<string>('INVITE_DOWNLOAD_LINK'),
    );
    const secureEnv =
      this.config.get<string>('INVITE_SMTP_SECURE') ??
      (port === 465 ? 'true' : 'false');
    const secure = secureEnv.toLowerCase().trim() === 'true';

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

  private static readonly FORGOT_PASSWORD_ACK_MESSAGE =
    'If the organisation code and email match our records, we sent a password reset link. The link works for 10 minutes only.';

  private async issuePasswordResetToken(
    orgCode: string,
    email: string,
  ): Promise<string> {
    const normalized = email.trim().toLowerCase();
    return this.jwtService.signAsync(
      {
        purpose: 'password-reset',
        orgCode: orgCode.trim(),
        email: normalized,
      },
      { expiresIn: '10m' },
    );
  }

  private async findAccountForReset(
    org: OrganizationDocument,
    email: string,
  ): Promise<{ kind: 'first-admin' | 'org-admin' | 'app-user'; uri: string } | null> {
    const normalized = email.trim().toLowerCase();
    const uri = org.orgDatabaseUri?.trim() ?? '';

    if (uri) {
      const orgAdmin = await this.tenant.findOrgAdminByEmail(uri, normalized);
      if (orgAdmin) {
        return { kind: 'org-admin', uri };
      }

      const appUser = await this.tenant.findOrgAppUserByEmail(uri, normalized);
      if (appUser) {
        return { kind: 'app-user', uri };
      }
    }

    const firstEmail = (org.firstAdminEmailNormalized ?? '').trim();
    if (firstEmail && firstEmail === normalized) {
      return { kind: 'first-admin', uri };
    }

    return null;
  }

  async requestPasswordReset(dto: SetupForgotPasswordDto) {
    let org: OrganizationDocument | null = null;
    try {
      await this.loadLicensedOrg(dto.orgCode);
      org = await this.orgs.findByOrgCode(dto.orgCode);
    } catch {
      org = null;
    }

    const email = dto.email.trim().toLowerCase();
    const account =
      org !== null ? await this.findAccountForReset(org, email) : null;

    if (account && org) {
      const token = await this.issuePasswordResetToken(dto.orgCode, email);
      const resetLink = buildPasswordResetLink(this.config, token);
      await this.sendPasswordResetEmail(email, dto.orgCode.trim(), resetLink);
    }

    return {
      ok: true as const,
      message: SetupService.FORGOT_PASSWORD_ACK_MESSAGE,
    };
  }

  async resetPasswordWithToken(dto: SetupResetPasswordDto) {
    let payload: {
      purpose?: string;
      orgCode?: string;
      email?: string;
    };

    try {
      payload = await this.jwtService.verifyAsync(dto.token);
    } catch {
      throw new BadRequestException(
        'This reset link is invalid or has expired. Request a new link from PartFinder (links work for 10 minutes).',
      );
    }

    if (
      payload.purpose !== 'password-reset' ||
      typeof payload.orgCode !== 'string' ||
      typeof payload.email !== 'string'
    ) {
      throw new BadRequestException(
        'This reset link is invalid or has expired. Request a new link from PartFinder (links work for 10 minutes).',
      );
    }

    await this.loadLicensedOrg(payload.orgCode);
    const org = await this.orgs.findByOrgCode(payload.orgCode);
    if (!org) {
      throw new BadRequestException(
        'This reset link is invalid or has expired. Request a new link from PartFinder (links work for 10 minutes).',
      );
    }

    const email = payload.email.trim().toLowerCase();
    const account = await this.findAccountForReset(org, email);
    if (!account) {
      throw new BadRequestException(
        'This reset link is invalid or has expired. Request a new link from PartFinder (links work for 10 minutes).',
      );
    }

    if (account.kind === 'first-admin' && !account.uri) {
      org.firstAdminTemporaryPasswordHash = this.hashTemporaryPassword(
        dto.newPassword,
      );
      await org.save();
      await this.refreshSessions.revokeAllForUser(payload.orgCode, email);
      return { ok: true as const, message: 'Password updated successfully.' };
    }

    if (!account.uri) {
      throw new BadRequestException(
        'Organization database is not configured yet. Complete setup or contact your administrator.',
      );
    }

    const passwordHash = await bcrypt.hash(dto.newPassword, BCRYPT_ROUNDS);
    await this.storeReplacedPassword(
      org,
      account.uri,
      email,
      passwordHash,
      account.kind,
    );
    await this.refreshSessions.revokeAllForUser(payload.orgCode, email);

    return { ok: true as const, message: 'Password updated successfully.' };
  }

  private async sendPasswordResetEmail(
    email: string,
    orgCode: string,
    resetLink: string,
  ) {
    const host =
      this.config.get<string>('INVITE_SMTP_HOST')?.trim() ||
      this.config.get<string>('EMAIL_HOST')?.trim() ||
      '';
    const port = Number(
      this.config.get<string>('INVITE_SMTP_PORT') ??
        this.config.get<string>('EMAIL_PORT') ??
        '587',
    );
    const user =
      this.config.get<string>('INVITE_SMTP_USER')?.trim() ||
      this.config.get<string>('EMAIL_USER')?.trim() ||
      '';
    const pass =
      this.config.get<string>('INVITE_SMTP_PASS') ??
      this.config.get<string>('EMAIL_PASS') ??
      '';
    const from =
      this.config.get<string>('INVITE_FROM_EMAIL')?.trim() ||
      this.config.get<string>('SMTP_FROM')?.trim() ||
      '';
    const fromName =
      this.config.get<string>('INVITE_FROM_NAME')?.trim() || 'PartFinder';
    const secureEnv =
      this.config.get<string>('INVITE_SMTP_SECURE') ??
      (port === 465 ? 'true' : 'false');
    const secure = secureEnv.toLowerCase().trim() === 'true';

    if (!host || !user || !pass || !from) {
      console.warn(
        '[sendPasswordResetEmail] SMTP not configured; reset link not emailed.',
      );
      return { sent: false, error: 'SMTP configuration is missing.' };
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
        subject: 'PartFinder password reset',
        text:
          `Hello,\n\n` +
          `We received a request to reset the PartFinder password for organisation ${orgCode} and this email address.\n\n` +
          `Create a new password using this link (valid for 10 minutes only):\n${resetLink}\n\n` +
          `If you did not request this, you can ignore this email. Your password will not change.\n\n` +
          `— PartFinder`,
        html:
          `<p>Hello,</p>` +
          `<p>We received a request to reset your <strong>PartFinder</strong> password for organisation <strong>${orgCode}</strong>.</p>` +
          `<p><a href="${resetLink}">Create a new password</a></p>` +
          `<p><strong>This link expires 10 minutes</strong> after it was sent. If it expires, use <em>Forgot password</em> in the PartFinder app to request a new link.</p>` +
          `<p>If you did not request this, you can ignore this email.</p>`,
      });

      return { sent: true, error: null as string | null };
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Failed to send reset email.';
      console.error('[sendPasswordResetEmail]', msg);
      return { sent: false, error: msg };
    }
  }

  async changePassword(dto: SetupChangePasswordDto) {
    await this.loadLicensedOrg(dto.orgCode);
    const org = await this.orgs.findByOrgCode(dto.orgCode);
    if (!org?.orgDatabaseUri?.trim()) {
      throw new BadRequestException('Organization database is not configured yet.');
    }

    const email = dto.email.trim().toLowerCase();
    const match = await this.currentPasswordMatches(org, email, dto.currentPassword);
    if (!match) {
      throw new BadRequestException('Current temporary password is invalid.');
    }

    const passwordHash = await bcrypt.hash(dto.newPassword, BCRYPT_ROUNDS);
    await this.storeReplacedPassword(
      org,
      org.orgDatabaseUri.trim(),
      email,
      passwordHash,
      match,
    );
    await this.refreshSessions.revokeAllForUser(dto.orgCode, email);
    return { ok: true as const };
  }

  private async currentPasswordMatches(
    org: OrganizationDocument,
    email: string,
    currentPassword: string,
  ): Promise<'first-admin' | 'org-admin' | 'app-user' | null> {
    const firstAdminEmail = (org.firstAdminEmailNormalized ?? '').trim();
    const firstAdminTempHash = (org.firstAdminTemporaryPasswordHash ?? '').trim();
    if (
      firstAdminEmail &&
      firstAdminTempHash &&
      email === firstAdminEmail &&
      (await this.passwordAccepted(currentPassword, firstAdminTempHash, 'pbkdf2'))
    ) {
      return 'first-admin';
    }

    const uri = org.orgDatabaseUri?.trim();
    if (!uri) {
      return null;
    }

    const orgAdmin = await this.tenant.findOrgAdminByEmail(uri, email);
    if (
      orgAdmin &&
      typeof orgAdmin.passwordHash === 'string' &&
      orgAdmin.passwordHash &&
      (await this.passwordAccepted(currentPassword, orgAdmin.passwordHash, 'bcrypt'))
    ) {
      return 'org-admin';
    }

    const doc = await this.tenant.findOrgAppUserByEmail(uri, email);
    if (!doc) {
      return null;
    }

    const tempHash =
      typeof doc.temporaryPasswordHash === 'string' ? doc.temporaryPasswordHash : '';
    if (tempHash && (await this.passwordAccepted(currentPassword, tempHash, 'pbkdf2'))) {
      return 'app-user';
    }

    const permanentHash = typeof doc.passwordHash === 'string' ? doc.passwordHash : '';
    if (
      permanentHash &&
      (await this.passwordAccepted(currentPassword, permanentHash, 'bcrypt'))
    ) {
      return 'app-user';
    }

    return null;
  }

  private async storeReplacedPassword(
    org: OrganizationDocument,
    uri: string,
    email: string,
    passwordHash: string,
    match: 'first-admin' | 'org-admin' | 'app-user',
  ): Promise<{ created: boolean; skipped: boolean }> {
    const conn = await this.tenant.getConnection(uri);
    const adminColl = this.tenant.orgAdminCollection(conn);
    const userColl = this.tenant.orgAppUsersCollection(conn);
    const appUser = await userColl.findOne({ emailNormalized: email });
    let created = false;
    let skipped = false;

    if (match === 'first-admin' || match === 'org-admin') {
      const adminName = org.name?.trim() ? `${org.name.trim()} Admin` : 'Organization Admin';
      created = await this.upsertOrgAdminPassword(adminColl, email, adminName, passwordHash);
      skipped = !created;
      if (match === 'first-admin' || (org.firstAdminEmailNormalized ?? '').trim() === email) {
        org.firstAdminTemporaryPasswordHash = null;
        await org.save();
      }
    } else if (appUser && appUser.role === 'Admin') {
      const adminName =
        typeof appUser.name === 'string' && appUser.name.trim()
          ? appUser.name.trim()
          : 'Organization Admin';
      const result = await this.tenant.createOrgAdminIfNone(uri, adminName, email, passwordHash);
      created = result.created;
      skipped = result.skipped;
      if ((org.firstAdminEmailNormalized ?? '').trim() === email && (created || skipped)) {
        org.firstAdminTemporaryPasswordHash = null;
        await org.save();
      }
    }

    if (appUser) {
      await userColl.updateOne(
        { emailNormalized: email },
        { $set: { temporaryPasswordHash: '', passwordHash } },
      );
    }

    return { created, skipped };
  }

  private async upsertOrgAdminPassword(
    adminColl: ReturnType<TenantMongoService['orgAdminCollection']>,
    email: string,
    adminName: string,
    passwordHash: string,
  ): Promise<boolean> {
    const existing = await adminColl.findOne({ email });
    await adminColl.updateOne(
      { email },
      {
        $set: { passwordHash, email },
        $setOnInsert: { name: adminName, createdAt: new Date() },
      },
      { upsert: true },
    );
    return !existing;
  }

  private passwordCandidates(password: string): string[] {
    const trimmed = password.trim();
    if (trimmed && trimmed !== password) {
      return [password, trimmed];
    }
    return [password];
  }

  private async passwordAccepted(
    password: string,
    stored: string,
    mode: 'pbkdf2' | 'bcrypt',
  ): Promise<boolean> {
    const hash = stored.trim();
    if (!hash) {
      return false;
    }

    for (const candidate of this.passwordCandidates(password)) {
      if (!candidate) {
        continue;
      }
      if (mode === 'pbkdf2') {
        if (this.orgs.verifyTemporaryPassword(candidate, hash)) {
          return true;
        }
        continue;
      }
      if (await this.bcryptMatches(candidate, hash)) {
        return true;
      }
    }

    return false;
  }

  private async bcryptMatches(password: string, hash: string): Promise<boolean> {
    if (!hash.startsWith('$2')) {
      return false;
    }
    try {
      return await bcrypt.compare(password, hash);
    } catch {
      return false;
    }
  }
}
