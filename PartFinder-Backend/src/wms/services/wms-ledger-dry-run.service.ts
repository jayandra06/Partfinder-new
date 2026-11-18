import { ForbiddenException, Injectable } from '@nestjs/common';
import type { WmsRequestContext } from '../wms.types';
import { assertWmsAdminWrite } from '../wms-scope.util';
import { isWmsLiveStockPostingEnabled } from '../wms-live-stock.util';
import { isWmsStagingStockPostingEnabled } from '../wms-staging-stock.util';
import { WmsTenantCollectionsService } from './wms-tenant-collections.service';

@Injectable()
export class WmsLedgerDryRunService {
  constructor(private readonly collections: WmsTenantCollectionsService) {}

  /** Always allowed — reports only, no balance writes. */
  async openingBalanceDryRun(ctx: WmsRequestContext) {
    assertWmsAdminWrite(ctx);
    const conn = await this.collections.connection(ctx.tenantUri);
    const db = conn.db;
    if (!db) {
      return { lines: [], unmapped: [], note: 'Tenant database unavailable.' };
    }

    const inventory = db.collection('partfinder_inventory');
    const legacyRows = await inventory.find({}).limit(5000).toArray();

    const idColl = await this.collections.skuIdentifiers(ctx.tenantUri);
    const legacyIds = await idColl
      .find({ namespace: 'legacyInventory', status: 'active' })
      .toArray();
    const legacyMap = new Map(
      legacyIds.map((d: Record<string, unknown>) => [
        String(d.value),
        String(d.skuId),
      ]),
    );

    const skus = await this.collections.skus(ctx.tenantUri);
    const skuDocs = await skus.find({ status: 'active' }).toArray();
    const skuUom = new Map(
      skuDocs.map((s: Record<string, unknown>) => [String(s.skuId), String(s.primaryUom)]),
    );

    const owners = await this.collections.stockOwners(ctx.tenantUri);
    const warehouses = await this.collections.warehouses(ctx.tenantUri);
    const defaultOwner = await owners.findOne({ isActive: { $ne: false } });
    const defaultWarehouse = await warehouses.findOne({ isActive: { $ne: false } });

    const lines: Array<Record<string, unknown>> = [];
    const unmapped: Array<Record<string, unknown>> = [];
    const invalidQuantities: Array<Record<string, unknown>> = [];
    const invalidUom: Array<Record<string, unknown>> = [];
    const duplicateCandidates: Array<Record<string, unknown>> = [];
    const partIdToLegacy = new Map<string, string[]>();

    for (const row of legacyRows) {
      const r = row as Record<string, unknown>;
      const mongoId = r._id ? String(r._id) : '';
      const partName = String(r.partName ?? '');
      const partId = String(r.partId ?? '');
      const qty = Number(r.quantity ?? 0);
      const legacyUom = String(r.uom ?? r.unit ?? '').trim();

      if (partId) {
        const arr = partIdToLegacy.get(partId) ?? [];
        arr.push(mongoId);
        partIdToLegacy.set(partId, arr);
      }

      if (!Number.isFinite(qty) || qty < 0) {
        invalidQuantities.push({ legacyInventoryId: mongoId, partName, quantity: r.quantity });
        continue;
      }

      const skuId = legacyMap.get(mongoId);
      if (skuId) {
        const primaryUom = skuUom.get(skuId) ?? 'EA';
        if (legacyUom && legacyUom.toUpperCase() !== primaryUom) {
          invalidUom.push({
            legacyInventoryId: mongoId,
            skuId,
            legacyUom,
            skuUom: primaryUom,
          });
        }
        lines.push({
          skuId,
          suggestedQty: qty,
          primaryUom,
          legacyInventoryId: mongoId,
          partName,
          status: 'mapped',
          proposedOwnerId: defaultOwner ? String(defaultOwner._id) : null,
          proposedWarehouseId: defaultWarehouse ? String(defaultWarehouse._id) : null,
          proposedLocationId: null,
        });
      } else {
        unmapped.push({
          legacyInventoryId: mongoId,
          partName,
          partId,
          quantity: qty,
          status: 'unmapped',
        });
      }
    }

    for (const [partId, ids] of partIdToLegacy.entries()) {
      if (ids.length > 1) {
        duplicateCandidates.push({ partId, legacyInventoryIds: ids });
      }
    }

    const proposedByWarehouseOwner: Array<Record<string, unknown>> = [];
    if (defaultOwner && defaultWarehouse) {
      const key = `${defaultWarehouse._id}:${defaultOwner._id}`;
      const totalQty = lines.reduce(
        (a, l) => a + Number(l.suggestedQty ?? 0),
        0,
      );
      proposedByWarehouseOwner.push({
        warehouseId: String(defaultWarehouse._id),
        ownerId: String(defaultOwner._id),
        lineCount: lines.length,
        totalSuggestedQty: totalQty,
        note: 'Assign explicit locations before staging opening balance post.',
      });
      void key;
    }

    return {
      totalSourceRows: legacyRows.length,
      livePostingEnabled: isWmsLiveStockPostingEnabled(),
      stagingPostingEnabled: isWmsStagingStockPostingEnabled(),
      mappedCount: lines.length,
      unmappedCount: unmapped.length,
      invalidQuantityCount: invalidQuantities.length,
      invalidUomCount: invalidUom.length,
      duplicateCandidateCount: duplicateCandidates.length,
      missingStockOwnerAssignment: !defaultOwner,
      missingDefaultWarehouse: !defaultWarehouse,
      unresolvedWarehouseLocations: lines.filter((l) => !l.proposedLocationId).length,
      lines,
      unmapped,
      invalidQuantities,
      invalidUom,
      duplicateCandidates,
      proposedOpeningByWarehouseOwner: proposedByWarehouseOwner,
      disclaimer:
        'Dry-run only. No wms_stock_balances or ledger transactions were written. Legacy inventory was not modified.',
    };
  }

  assertLivePostingBlocked(): void {
    if (!isWmsLiveStockPostingEnabled()) {
      throw new ForbiddenException(
        'Live stock posting is disabled (WMS_LIVE_STOCK_POSTING). Dry-run endpoints only.',
      );
    }
  }
}