import { ForbiddenException } from '@nestjs/common';
import type { WmsRequestContext } from './wms.types';

export function assertWarehouseAccess(
  ctx: WmsRequestContext,
  warehouseId: string,
): void {
  if (ctx.allowedWarehouseIds === 'all') {
    return;
  }
  if (!ctx.allowedWarehouseIds.includes(warehouseId)) {
    throw new ForbiddenException('Warehouse access denied.');
  }
}

export function assertOwnerAccess(ctx: WmsRequestContext, ownerId: string): void {
  if (ctx.allowedOwnerIds === 'all') {
    return;
  }
  if (!ctx.allowedOwnerIds.includes(ownerId)) {
    throw new ForbiddenException('Stock owner access denied.');
  }
}

export function assertWmsAdminWrite(ctx: WmsRequestContext): void {
  if (!ctx.isWmsAdmin) {
    throw new ForbiddenException('WMS setup admin permission required.');
  }
}