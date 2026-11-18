import { Injectable } from '@nestjs/common';
import type { WmsRequestContext } from '../wms.types';
import {
  assertOwnerAccess,
  assertWarehouseAccess,
} from '../wms-scope.util';
import { buildBalanceKey, computeAtp } from '../wms-balance-key.util';
import { WmsTenantCollectionsService } from './wms-tenant-collections.service';

@Injectable()
export class WmsInventoryReadService {
  constructor(private readonly collections: WmsTenantCollectionsService) {}

  async listBalances(
    ctx: WmsRequestContext,
    opts: {
      warehouseId?: string;
      ownerId?: string;
      locationId?: string;
      skuId?: string;
      skip?: number;
      limit?: number;
    },
  ) {
    await this.collections.ensurePhase02B1Indexes(ctx.tenantUri);
    const coll = await this.collections.stockBalances(ctx.tenantUri);
    const filter: Record<string, unknown> = {};
    if (opts.warehouseId) {
      assertWarehouseAccess(ctx, opts.warehouseId);
      filter.warehouseId = opts.warehouseId;
    }
    if (opts.ownerId) {
      assertOwnerAccess(ctx, opts.ownerId);
      filter.ownerId = opts.ownerId;
    }
    if (opts.locationId) filter.locationId = opts.locationId;
    if (opts.skuId) filter.skuKey = opts.skuId;

    if (ctx.allowedOwnerIds !== 'all' && !opts.ownerId) {
      filter.ownerId = { $in: ctx.allowedOwnerIds };
    }
    if (ctx.allowedWarehouseIds !== 'all' && !opts.warehouseId) {
      filter.warehouseId = { $in: ctx.allowedWarehouseIds };
    }

    const skip = Math.max(0, opts.skip ?? 0);
    const limit = Math.min(500, Math.max(1, opts.limit ?? 100));
    const [items, total] = await Promise.all([
      coll.find(filter).sort({ updatedAt: -1 }).skip(skip).limit(limit).toArray(),
      coll.countDocuments(filter),
    ]);

    return {
      items: items.map((d: Record<string, unknown>) => this.mapBalance(d)),
      total,
      skip,
      limit,
    };
  }

  async listTransactions(
    ctx: WmsRequestContext,
    opts: {
      warehouseId?: string;
      ownerId?: string;
      skuId?: string;
      type?: string;
      skip?: number;
      limit?: number;
    },
  ) {
    await this.collections.ensurePhase02B1Indexes(ctx.tenantUri);
    const coll = await this.collections.stockTransactions(ctx.tenantUri);
    const filter: Record<string, unknown> = {};
    if (opts.warehouseId) {
      assertWarehouseAccess(ctx, opts.warehouseId);
      filter.warehouseId = opts.warehouseId;
    }
    if (opts.ownerId) {
      assertOwnerAccess(ctx, opts.ownerId);
      filter.ownerId = opts.ownerId;
    }
    if (opts.type) filter.type = opts.type;
    if (opts.skuId) filter['lines.skuId'] = opts.skuId;

    if (ctx.allowedOwnerIds !== 'all' && !opts.ownerId) {
      filter.ownerId = { $in: ctx.allowedOwnerIds };
    }
    if (ctx.allowedWarehouseIds !== 'all' && !opts.warehouseId) {
      filter.warehouseId = { $in: ctx.allowedWarehouseIds };
    }

    const skip = Math.max(0, opts.skip ?? 0);
    const limit = Math.min(500, Math.max(1, opts.limit ?? 50));
    const [items, total] = await Promise.all([
      coll.find(filter).sort({ createdAt: -1 }).skip(skip).limit(limit).toArray(),
      coll.countDocuments(filter),
    ]);
    return { items: items.map((d: Record<string, unknown>) => this.mapTxn(d)), total, skip, limit };
  }

  async computeAtpForSku(
    ctx: WmsRequestContext,
    ownerId: string,
    warehouseId: string,
    skuId: string,
  ) {
    assertOwnerAccess(ctx, ownerId);
    assertWarehouseAccess(ctx, warehouseId);
    const coll = await this.collections.stockBalances(ctx.tenantUri);
    const rows = await coll
      .find({ ownerId, warehouseId, skuKey: skuId })
      .toArray();
    let onHand = 0;
    let reserved = 0;
    let blockedQty = 0;
    for (const r of rows) {
      const row = r as Record<string, unknown>;
      onHand += Number(row.onHand ?? 0);
      reserved += Number(row.reserved ?? 0);
      blockedQty += Number(row.blockedQty ?? 0);
    }
    return {
      ownerId,
      warehouseId,
      skuId,
      onHand,
      reserved,
      blockedQty,
      available: computeAtp(onHand, reserved, blockedQty),
    };
  }

  async hasStockForSku(ctx: WmsRequestContext, skuId: string): Promise<boolean> {
    await this.collections.ensurePhase02B1Indexes(ctx.tenantUri);
    const coll = await this.collections.stockBalances(ctx.tenantUri);
    const doc = await coll.findOne({
      skuKey: skuId,
      onHand: { $gt: 0 },
    });
    return Boolean(doc);
  }

  async countTxnReferences(ctx: WmsRequestContext, skuId: string): Promise<number> {
    const coll = await this.collections.stockTransactions(ctx.tenantUri);
    return coll.countDocuments({ 'lines.skuId': skuId });
  }

  private mapBalance(doc: Record<string, unknown>) {
    const onHand = Number(doc.onHand ?? 0);
    const reserved = Number(doc.reserved ?? 0);
    const blockedQty = Number(doc.blockedQty ?? 0);
    return {
      balanceKey: String(doc.balanceKey ?? ''),
      ownerId: String(doc.ownerId),
      warehouseId: String(doc.warehouseId),
      locationId: String(doc.locationId),
      skuId: String(doc.skuKey),
      lotId: doc.lotId ?? null,
      serialId: doc.serialId ?? null,
      primaryUom: String(doc.primaryUom ?? ''),
      onHand,
      reserved,
      blockedQty,
      available: computeAtp(onHand, reserved, blockedQty),
      version: Number(doc.version ?? 0),
      updatedAt: doc.updatedAt,
    };
  }

  private mapTxn(doc: Record<string, unknown>) {
    return {
      txnId: String(doc.txnId),
      type: String(doc.type),
      ownerId: String(doc.ownerId),
      warehouseId: String(doc.warehouseId),
      lines: doc.lines,
      refs: doc.refs ?? {},
      idempotencyKey: doc.idempotencyKey ?? null,
      reversalOf: doc.reversalOf ?? null,
      createdBy: String(doc.createdBy ?? ''),
      createdAt: doc.createdAt,
    };
  }

  balanceKey(parts: Parameters<typeof buildBalanceKey>[0]) {
    return buildBalanceKey(parts);
  }
}