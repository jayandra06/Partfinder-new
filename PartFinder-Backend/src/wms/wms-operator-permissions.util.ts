import { ForbiddenException } from '@nestjs/common';
import type { WmsOperatorGrants, WmsRequestContext } from './wms.types';

export function defaultOperatorGrants(): WmsOperatorGrants {
  return { receiving: false, qc: false, putaway: false, supervise: false };
}

export function adminOperatorGrants(): WmsOperatorGrants {
  return { receiving: true, qc: true, putaway: true, supervise: true };
}

export function assertReceivingPermission(ctx: WmsRequestContext): void {
  if (ctx.isWmsAdmin || ctx.operatorPermissions.receiving) return;
  throw new ForbiddenException('Receiving operator permission required.');
}

export function assertQcPermission(ctx: WmsRequestContext): void {
  if (ctx.isWmsAdmin || ctx.operatorPermissions.qc || ctx.operatorPermissions.supervise)
    return;
  throw new ForbiddenException('Quality control permission required.');
}

export function assertPutawayPermission(ctx: WmsRequestContext): void {
  if (ctx.isWmsAdmin || ctx.operatorPermissions.putaway) return;
  throw new ForbiddenException('Put-away operator permission required.');
}