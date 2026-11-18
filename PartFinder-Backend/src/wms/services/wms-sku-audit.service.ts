import { Injectable } from '@nestjs/common';
import type { WmsRequestContext } from '../wms.types';
import { WMS_COLLECTIONS } from '../wms.types';
import { WmsTenantCollectionsService } from './wms-tenant-collections.service';

export type WmsSkuAuditAction =
  | 'sku.created'
  | 'sku.updated'
  | 'sku.identifier.added'
  | 'sku.identifier.superseded'
  | 'sku.merged'
  | 'sku.retired';

@Injectable()
export class WmsSkuAuditService {
  constructor(private readonly collections: WmsTenantCollectionsService) {}

  async append(
    ctx: WmsRequestContext,
    action: WmsSkuAuditAction,
    skuId: string,
    detail: Record<string, unknown> = {},
  ): Promise<void> {
    const coll = await this.collections.skuAuditLog(ctx.tenantUri);
    await coll.insertOne({
      action,
      skuId,
      detail,
      actorEmail: ctx.email,
      createdAt: new Date(),
    });
  }
}