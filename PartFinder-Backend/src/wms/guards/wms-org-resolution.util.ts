import { ForbiddenException } from '@nestjs/common';

const ORG_CODE_PATTERN = /^\d{6}$/;

export type JwtOrgUser = {
  email: string;
  orgCode?: string;
};

/**
 * Resolve the authoritative organization for WMS.
 * The JWT org claim is the source of truth; the client header is a consistency check only.
 */
export function resolveAuthoritativeOrgCode(
  jwtUser: JwtOrgUser,
  headerOrgRaw: string,
): string {
  const jwtOrg = (jwtUser.orgCode ?? '').trim();
  if (!jwtOrg || !ORG_CODE_PATTERN.test(jwtOrg)) {
    throw new ForbiddenException(
      'Organization-scoped session required for Warehouse Management. Sign in again from the PartFinder app.',
    );
  }

  const headerOrg = headerOrgRaw.trim();
  if (headerOrg) {
    if (!ORG_CODE_PATTERN.test(headerOrg)) {
      throw new ForbiddenException('Valid X-Org-Id header is required.');
    }
    if (headerOrg !== jwtOrg) {
      throw new ForbiddenException('Organization does not match session token.');
    }
  }

  return jwtOrg;
}