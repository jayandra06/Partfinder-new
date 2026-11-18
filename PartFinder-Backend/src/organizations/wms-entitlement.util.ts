import type { Organization } from './schemas/organization.schema';

/**
 * WMS is a separately licensed product. Only organizations with an explicit
 * `wmsEnabled: true` may call protected WMS routes.
 *
 * Legacy documents without the field are treated as **not** entitled (opt-in).
 * Pilot/testing orgs must set `wmsEnabled: true` in the platform DB.
 */
export function isWmsProductEntitled(
  org: Pick<Organization, 'wmsEnabled'>,
): boolean {
  return org.wmsEnabled === true;
}