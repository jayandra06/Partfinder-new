import {
  CanActivate,
  ExecutionContext,
  Injectable,
  UnauthorizedException,
} from '@nestjs/common';
import { OrganizationsService } from '../../organizations/organizations.service';
import { TenantMongoService } from '../../setup/tenant-mongo.service';
import { WmsContextResolverService } from '../services/wms-context-resolver.service';
import type { WmsRequestContext } from '../wms.types';
import { resolveAuthoritativeOrgCode } from './wms-org-resolution.util';

@Injectable()
export class WmsSessionGuard implements CanActivate {
  private readonly resolver: WmsContextResolverService;

  constructor(orgs: OrganizationsService, tenant: TenantMongoService) {
    this.resolver = new WmsContextResolverService(orgs, tenant);
  }

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

    req.wmsContext = await this.resolver.resolveContext(orgCode, jwtUser.email);
    return true;
  }

  private headerString(value: string | string[] | undefined): string {
    if (Array.isArray(value)) {
      return (value[0] ?? '').trim();
    }
    return (value ?? '').trim();
  }
}