import {
  CanActivate,
  ExecutionContext,
  ForbiddenException,
  Injectable,
  UnauthorizedException,
} from '@nestjs/common';
import { OrganizationsService } from '../../organizations/organizations.service';
import { TenantMongoService } from '../../setup/tenant-mongo.service';
import type { WmsRequestContext, WmsRole } from '../wms.types';
import { WMS_COLLECTIONS } from '../wms.types';

@Injectable()
export class WmsSessionGuard implements CanActivate {
  constructor(
    private readonly orgs: OrganizationsService,
    private readonly tenant: TenantMongoService,
  ) {}

  async canActivate(context: ExecutionContext): Promise<boolean> {
    const req = context.switchToHttp().getRequest<{
      user?: { userId: string; email: string };
      headers: Record<string, string | string[] | undefined>;
      wmsContext?: WmsRequestContext;
    }>();

    const jwtUser = req.user;
    if (!jwtUser?.email) {
      throw new UnauthorizedException('Authentication required.');
    }

    const headerOrg = this.headerString(req.headers['x-org-id']);
    if (!headerOrg || !/^\d{6}$/.test(headerOrg)) {
      throw new ForbiddenException('Valid X-Org-Id header is required.');
    }

    const payload = req.user as { email: string; orgCode?: string };
    if (payload.orgCode && payload.orgCode !== headerOrg) {
      throw new ForbiddenException('Organization does not match session token.');
    }

    const license = await this.orgs.verifyLicense(headerOrg);
    if (!license.valid) {
      throw new ForbiddenException(license.message ?? 'License invalid.');
    }

    const org = await this.orgs.findByOrgCode(headerOrg);
    if (!org?.orgDatabaseUri?.trim()) {
      throw new ForbiddenException('Organization database is not configured.');
    }

    const wmsEnabled = org.wmsEnabled !== false;
    if (!wmsEnabled) {
      throw new ForbiddenException(
        'Warehouse Management is not enabled for this subscription.',
      );
    }

    const uri = org.orgDatabaseUri.trim();
    const email = jwtUser.email.trim().toLowerCase();

    const role = await this.resolveRole(uri, email);
    const grants = await this.loadGrants(uri, email);

    const isWmsAdmin = role === 'Admin';
    const ctx: WmsRequestContext = {
      orgCode: headerOrg,
      organizationName: org.name,
      tenantUri: uri,
      email,
      role,
      isWmsAdmin,
      allowedWarehouseIds: isWmsAdmin ? 'all' : grants.warehouseIds,
      allowedOwnerIds: isWmsAdmin ? 'all' : grants.ownerIds,
      wmsEnabled,
      maxWmsWarehouses: org.maxWmsWarehouses ?? 10,
      maxStockOwners: org.maxStockOwners ?? 50,
    };

    req.wmsContext = ctx;
    return true;
  }

  private headerString(value: string | string[] | undefined): string {
    if (Array.isArray(value)) {
      return (value[0] ?? '').trim();
    }
    return (value ?? '').trim();
  }

  private async resolveRole(uri: string, email: string): Promise<WmsRole> {
    const admin = await this.tenant.findOrgAdminByEmail(uri, email);
    if (admin) {
      return 'Admin';
    }
    const appUser = await this.tenant.findOrgAppUserByEmail(uri, email);
    if (!appUser) {
      throw new ForbiddenException('User is not a member of this organization.');
    }
    const role =
      typeof appUser.role === 'string' && appUser.role.trim()
        ? appUser.role.trim()
        : 'Employee';
    if (role.toLowerCase() === 'admin') {
      return 'Admin';
    }
    return 'Employee';
  }

  private async loadGrants(
    uri: string,
    email: string,
  ): Promise<{ warehouseIds: string[]; ownerIds: string[] }> {
    const conn = await this.tenant.getConnection(uri);
    const db = conn.db;
    if (!db) {
      return { warehouseIds: [], ownerIds: [] };
    }
    const doc = await db.collection(WMS_COLLECTIONS.operatorGrants).findOne({ email });
    const warehouseIds = Array.isArray(doc?.warehouseIds)
      ? doc.warehouseIds.map(String)
      : [];
    const ownerIds = Array.isArray(doc?.ownerIds)
      ? doc.ownerIds.map(String)
      : [];
    return { warehouseIds, ownerIds };
  }
}