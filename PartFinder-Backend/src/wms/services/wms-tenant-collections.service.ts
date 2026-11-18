import { Injectable } from '@nestjs/common';
import type { Connection } from 'mongoose';

/** Native driver collection (avoids BSON duplicate-type friction in TS). */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type WmsCollection = any;
import { TenantMongoService } from '../../setup/tenant-mongo.service';
import { WMS_COLLECTIONS } from '../wms.types';

@Injectable()
export class WmsTenantCollectionsService {
  constructor(private readonly tenant: TenantMongoService) {}

  async connection(uri: string): Promise<Connection> {
    return this.tenant.getConnection(uri);
  }

  warehouses(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.warehouses);
  }

  stockOwners(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.stockOwners);
  }

  zones(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.zones);
  }

  locations(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.locations);
  }

  skus(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.skus);
  }

  skuIdentifiers(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.skuIdentifiers);
  }

  skuAuditLog(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.skuAuditLog);
  }

  uomDefinitions(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.uomDefinitions);
  }

  uomTemplateMappings(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.uomTemplateMappings);
  }

  erpConnectors(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.erpConnectors);
  }

  inboundMessages(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.inboundMessages);
  }

  stockBalances(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.stockBalances);
  }

  stockTransactions(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.stockTransactions);
  }

  idempotencyKeys(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.idempotencyKeys);
  }

  erpSkuMappings(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.erpSkuMappings);
  }

  erpLocationMappings(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.erpLocationMappings);
  }

  outboxEvents(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.outboxEvents);
  }

  expectedReceipts(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.expectedReceipts);
  }

  goodsReceipts(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.goodsReceipts);
  }

  receiptLines(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.receiptLines);
  }

  qcInspections(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.qcInspections);
  }

  putawayTasks(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.putawayTasks);
  }

  receivingAuditLog(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.receivingAuditLog);
  }

  stagingGateEvidence(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.stagingGateEvidence);
  }

  async ensureIndexes(uri: string): Promise<void> {
    const wh = await this.warehouses(uri);
    const owners = await this.stockOwners(uri);
    const zones = await this.zones(uri);
    const locs = await this.locations(uri);

    await wh.createIndex({ code: 1 }, { unique: true });
    await owners.createIndex({ code: 1 }, { unique: true });
    await zones.createIndex({ warehouseId: 1, code: 1 }, { unique: true });
    await locs.createIndex({ warehouseId: 1, path: 1 }, { unique: true });
    await locs.createIndex({ warehouseId: 1, zoneId: 1 });
  }

  async ensurePhase02Indexes(uri: string): Promise<void> {
    await this.ensureIndexes(uri);

    const skus = await this.skus(uri);
    const ids = await this.skuIdentifiers(uri);
    const audit = await this.skuAuditLog(uri);
    const uom = await this.uomDefinitions(uri);
    const uomTpl = await this.uomTemplateMappings(uri);
    const erp = await this.erpConnectors(uri);
    const inbound = await this.inboundMessages(uri);

    await skus.createIndex({ skuId: 1 }, { unique: true });
    await skus.createIndex({ status: 1 });
    await skus.createIndex({ variantGroupId: 1 }, { sparse: true });

    await ids.createIndex(
      { scopeKey: 1 },
      {
        unique: true,
        partialFilterExpression: { status: 'active' },
      },
    );
    await ids.createIndex({ skuId: 1, namespace: 1 });
    await ids.createIndex({ namespace: 1, value: 1 });

    await audit.createIndex({ skuId: 1, createdAt: -1 });
    await audit.createIndex({ createdAt: -1 });

    await uom.createIndex({ code: 1 }, { unique: true });
    await uomTpl.createIndex(
      { templateId: 1, sourceLabelNorm: 1 },
      { unique: true },
    );

    await erp.createIndex({ erpConnectorId: 1 }, { unique: true });

    await inbound.createIndex(
      { erpConnectorId: 1, idempotencyKey: 1 },
      { unique: true },
    );
  }

  async ensurePhase02B1Indexes(uri: string): Promise<void> {
    await this.ensurePhase02Indexes(uri);

    const balances = await this.stockBalances(uri);
    const txns = await this.stockTransactions(uri);
    const idem = await this.idempotencyKeys(uri);
    const skuMap = await this.erpSkuMappings(uri);
    const locMap = await this.erpLocationMappings(uri);
    const outbox = await this.outboxEvents(uri);

    await balances.createIndex({ balanceKey: 1 }, { unique: true });
    await balances.createIndex({ ownerId: 1, warehouseId: 1, skuKey: 1 });
    await balances.createIndex({ warehouseId: 1, locationId: 1 });
    await balances.createIndex({ skuKey: 1 });

    await txns.createIndex({ txnId: 1 }, { unique: true });
    await txns.createIndex({ createdAt: -1 });
    await txns.createIndex({ ownerId: 1, warehouseId: 1, createdAt: -1 });
    await txns.createIndex({ idempotencyKey: 1 }, { sparse: true });
    await txns.createIndex({ 'lines.skuId': 1 });

    await idem.createIndex({ key: 1 }, { unique: true });
    await idem.createIndex({ expiresAt: 1 }, { expireAfterSeconds: 0 });

    await skuMap.createIndex(
      { erpConnectorId: 1, externalMaterialId: 1 },
      { unique: true },
    );
    await skuMap.createIndex({ skuId: 1 });

    await locMap.createIndex(
      { erpConnectorId: 1, externalLocationId: 1 },
      { unique: true },
    );

    await outbox.createIndex({ eventId: 1 }, { unique: true });
    await outbox.createIndex({ status: 1, createdAt: 1 });
    await outbox.createIndex({ txnId: 1 });
  }

  async ensurePhase03AIndexes(uri: string): Promise<void> {
    await this.ensurePhase02B1Indexes(uri);

    const expected = await this.expectedReceipts(uri);
    const receipts = await this.goodsReceipts(uri);
    const lines = await this.receiptLines(uri);
    const qc = await this.qcInspections(uri);
    const putaway = await this.putawayTasks(uri);
    const audit = await this.receivingAuditLog(uri);
    const gate = await this.stagingGateEvidence(uri);

    await expected.createIndex({ expectedReceiptId: 1 }, { unique: true });
    await expected.createIndex({ warehouseId: 1, ownerId: 1, status: 1 });

    await receipts.createIndex({ goodsReceiptId: 1 }, { unique: true });
    await receipts.createIndex({ idempotencyKey: 1 }, { unique: true, sparse: true });
    await receipts.createIndex({ expectedReceiptId: 1 });

    await lines.createIndex({ receiptLineId: 1 }, { unique: true });
    await lines.createIndex({ goodsReceiptId: 1 });

    await qc.createIndex({ qcId: 1 }, { unique: true });
    await qc.createIndex({ receiptLineId: 1 }, { unique: true });

    await putaway.createIndex({ putawayTaskId: 1 }, { unique: true });
    await putaway.createIndex({ status: 1, warehouseId: 1 });

    await audit.createIndex({ createdAt: -1 });
    await gate.createIndex({ recordedAt: -1 });
  }

  private async collection(uri: string, name: string): Promise<WmsCollection> {
    const conn = await this.tenant.getConnection(uri);
    const db = conn.db;
    if (!db) {
      throw new Error('Tenant database unavailable');
    }
    return db.collection(name);
  }
}