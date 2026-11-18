import {
  CanActivate,
  ExecutionContext,
  ForbiddenException,
  Injectable,
  UnauthorizedException,
} from '@nestjs/common';
import { OrganizationsService } from '../../organizations/organizations.service';
import { isWmsProductEntitled } from '../../organizations/wms-entitlement.util';
import { TenantMongoService } from '../../setup/tenant-mongo.service';
import type {
  WmsOperatorGrants,
  WmsRequestContext,
  WmsRole,
} from '../wms.types';
import { WMS_COLLECTIONS } from '../wms.types';
import {
  adminOperatorGrants,
  defaultOperatorGrants,
} from '../wms-operator-permissions.util';
import { resolveAuthoritativeOrgCode } from './wms-org-resolution.util';

@Injectable()
export class WmsSessionGuard implements CanActivate {
  constructor(
    private readonly orgs: OrganizationsService,
    private readonly tenant: TenantMongoService,
  ) {}

  async canActivate(context: ExecutionContext): Promise<boolean> {
    const req = context.switchToHttp().getRequest<{
      user?: {
        userId: string;
        email: string;
        orgCode?: string;
      };
      headers: Record<string, string | string[] | undefined>;
      wmsContext?: WmsRequestContext;
    }>();

    const jwtUser = req.user;
    if (!jwtUser?.email) {
      throw new UnauthorizedException('Authentication required.');
    }

    const headerOrg = this.headerString(req.headers['x-org-id']);
    const orgCode = resolveAuthoritativeOrgCode(
      { email: jwtUser.email, orgCode: jwtUser.orgCode },
      headerOrg,
    );

    const license = await this.orgs.verifyLicense(orgCode);
    if (!license.valid) {
      throw new ForbiddenException(license.message ?? 'License invalid.');
    }

    if (!license.wmsEnabled) {
      throw new ForbiddenException(
        'Warehouse Management is not enabled for this subscription.',
      );
    }

    const org = await this.orgs.findByOrgCode(orgCode);
    if (!org?.orgDatabaseUri?.trim()) {
      throw new ForbiddenException('Organization database is not configured.');
    }

    if (!isWmsProductEntitled(org)) {
      throw new ForbiddenException(
        'Warehouse Management is not enabled for this subscription.',
      );
    }

    const uri = org.orgDatabaseUri.trim();
    const email = jwtUser.email.trim().toLowerCase();

    const role = await this.resolveRole(uri, email);
    const grants = await this.loadGrants(uri, email);

    const isWmsAdmin = role === 'Admin';
    if (
      !isWmsAdmin &&
      grants.warehouseIds.length === 0 &&
      grants.ownerIds.length === 0
    ) {
      throw new ForbiddenException('No WMS operator grants assigned.');
    }

    const operatorPermissions = isWmsAdmin
      ? adminOperatorGrants()
      : this.mapPermissions(grants.raw);

    const ctx: WmsRequestContext = {
      orgCode,
      organizationName: org.name,
      tenantUri: uri,
      email,
      role,
      isWmsAdmin,
      allowedWarehouseIds: isWmsAdmin ? 'all' : grants.warehouseIds,
      allowedOwnerIds: isWmsAdmin ? 'all' : grants.ownerIds,
      wmsEnabled: true,
      maxWmsWarehouses: org.maxWmsWarehouses ?? 10,
      maxStockOwners: org.maxStockOwners ?? 50,
      operatorPermissions,
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
  ): Promise<{
    warehouseIds: string[];
    ownerIds: string[];
    raw: Record<string, unknown> | null;
  }> {
    const conn = await this.tenant.getConnection(uri);
    const db = conn.db;
    if (!db) {
      return { warehouseIds: [], ownerIds: [], raw: null };
    }
    const doc = await db.collection(WMS_COLLECTIONS.operatorGrants).findOne({ email });
    const warehouseIds = Array.isArray(doc?.warehouseIds)
      ? doc.warehouseIds.map(String)
      : [];
    const ownerIds = Array.isArray(doc?.ownerIds)
      ? doc.ownerIds.map(String)
      : [];
    return {
      warehouseIds,
      ownerIds,
      raw: doc as Record<string, unknown> | null,
    };
  }

  private mapPermissions(doc: Record<string, unknown> | null): WmsOperatorGrants {
    const base = defaultOperatorGrants();
    if (!doc) return base;
    const p = doc.permissions as Record<string, unknown> | undefined;
    if (!p) return base;
    return {
      receiving: p.receiving === true,
      qc: p.qc === true,
      putaway: p.putaway === true,
      supervise: p.supervise === true,
    };
  }
}