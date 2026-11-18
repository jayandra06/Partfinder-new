/**
 * Disposable MongoDB replica-set integration tests (not staging-tenant evidence).
 * Run: npm test -- --testPathPatterns=receiving-workflow.integration
 */
import { Test, type TestingModule } from '@nestjs/testing';
import { MongoMemoryReplSet } from 'mongodb-memory-server';
import { Types } from 'mongoose';
import { TenantMongoService } from '../setup/tenant-mongo.service';
import { WmsReceivingService } from './services/wms-receiving.service';
import { WmsInventoryPostingService } from './services/wms-inventory-posting.service';
import { WmsStagingGateService } from './services/wms-staging-gate.service';
import { WmsTenantCollectionsService } from './services/wms-tenant-collections.service';
import { WmsTransactionGuardService } from './services/wms-transaction-guard.service';
import { WmsDiagnosticsService } from './services/wms-diagnostics.service';
import type { WmsRequestContext } from './wms.types';
import { ForbiddenException } from '@nestjs/common';

describe('Wms receiving workflow (integration)', () => {
  let replSet: MongoMemoryReplSet;
  let moduleRef: TestingModule;
  let tenantMongo: TenantMongoService;
  let tenantUri: string;
  let receiving: WmsReceivingService;
  let posting: WmsInventoryPostingService;
  let collections: WmsTenantCollectionsService;
  let whId: string;
  let ownerA: string;
  let ownerB: string;
  let recvLoc: string;
  let storeLoc: string;
  let skuId: string;
  const operatorCtx = (): WmsRequestContext => ({
    orgCode: '999999',
    organizationName: 'Test',
    tenantUri,
    email: 'op@client-a.test',
    role: 'Employee',
    isWmsAdmin: false,
    allowedWarehouseIds: [whId],
    allowedOwnerIds: [ownerA],
    wmsEnabled: true,
    maxWmsWarehouses: 10,
    maxStockOwners: 50,
    operatorPermissions: { receiving: true, qc: true, putaway: true, supervise: false },
  });

  const clientBCtx = (): WmsRequestContext => ({
    ...operatorCtx(),
    email: 'op@client-b.test',
    allowedOwnerIds: [ownerB],
  });

  beforeAll(async () => {
    process.env.WMS_STAGING_STOCK_POSTING = 'true';
    process.env.WMS_LIVE_STOCK_POSTING = 'false';
    replSet = await MongoMemoryReplSet.create({ replSet: { count: 1 } });
    tenantUri = replSet.getUri('wms_integration_test');

    moduleRef = await Test.createTestingModule({
      providers: [
        TenantMongoService,
        WmsTenantCollectionsService,
        WmsDiagnosticsService,
        WmsTransactionGuardService,
        WmsInventoryPostingService,
        WmsStagingGateService,
        WmsReceivingService,
      ],
    }).compile();
    tenantMongo = moduleRef.get(TenantMongoService);
    collections = moduleRef.get(WmsTenantCollectionsService);
    posting = moduleRef.get(WmsInventoryPostingService);
    receiving = moduleRef.get(WmsReceivingService);

    whId = new Types.ObjectId().toHexString();
    ownerA = new Types.ObjectId().toHexString();
    ownerB = new Types.ObjectId().toHexString();
    recvLoc = new Types.ObjectId().toHexString();
    storeLoc = new Types.ObjectId().toHexString();
    skuId = '01HQTESTSKU001';

    const conn = await collections.connection(tenantUri);
    await conn.db.collection('wms_warehouses').insertOne({
      _id: new Types.ObjectId(whId),
      code: 'WH1',
      name: 'Main',
      isActive: true,
    });
    await conn.db.collection('wms_stock_owners').insertMany([
      { _id: new Types.ObjectId(ownerA), code: 'A', name: 'Client A', isActive: true },
      { _id: new Types.ObjectId(ownerB), code: 'B', name: 'Client B', isActive: true },
    ]);
    await conn.db.collection('wms_locations').insertMany([
      { _id: new Types.ObjectId(recvLoc), warehouseId: whId, path: 'RECV-01', type: 'receiving', isActive: true },
      { _id: new Types.ObjectId(storeLoc), warehouseId: whId, path: 'A-01-01', type: 'storage', isActive: true },
    ]);
    await conn.db.collection('wms_skus').insertOne({
      skuId,
      displayName: 'Filter',
      primaryUom: 'EA',
      status: 'active',
    });
    await conn.db.collection('wms_staging_gate_evidence').insertOne({
      tenantLabel: 'integration',
      supported: true,
      detail: 'mongodb-memory-server replSet',
      recordedAt: new Date(),
    });
  }, 180_000);

  afterAll(async () => {
    await tenantMongo?.onModuleDestroy();
    await moduleRef?.close();
    if (replSet) {
      await replSet.stop();
    }
    delete process.env.WMS_STAGING_STOCK_POSTING;
  });

  it('atomic receipt + QC block, partial QC, put-away, idempotency', async () => {
    const ctx = operatorCtx();
    const expected = await receiving.createExpectedReceipt(ctx, {
      ownerId: ownerA,
      warehouseId: whId,
      externalPoId: 'PO-100-FILTERS',
    });
    await receiving.addExpectedLines(ctx, expected.expectedReceiptId, {
      lines: [{ skuId, qtyExpected: 100, primaryUom: 'EA' }],
    });

    const idem = 'idem-del-001-filter-100';
    const first = await receiving.confirmGoodsReceipt(ctx, {
      idempotencyKey: idem,
      ownerId: ownerA,
      warehouseId: whId,
      receivingLocationId: recvLoc,
      expectedReceiptId: expected.expectedReceiptId,
      lines: [{ skuId, qtyReceived: 60 }],
    });
    expect(first.duplicate).toBe(false);

    const dup = await receiving.confirmGoodsReceipt(ctx, {
      idempotencyKey: idem,
      ownerId: ownerA,
      warehouseId: whId,
      receivingLocationId: recvLoc,
      expectedReceiptId: expected.expectedReceiptId,
      lines: [{ skuId, qtyReceived: 60 }],
    });
    expect(dup.duplicate).toBe(true);

    const lines = await receiving.listReceiptLines(ctx, first.goodsReceiptId);
    const lineId = String((lines.items[0] as { receiptLineId: string }).receiptLineId);

    await receiving.applyQc(ctx, lineId, {
      status: 'partial',
      qtyAccepted: 55,
      qtyRejected: 5,
      note: 'Visual inspection',
    });

    const putaways = await receiving.listPutaway(ctx, whId);
    const taskId = String((putaways.items[0] as { putawayTaskId: string }).putawayTaskId);
    await receiving.completePutaway(ctx, taskId, {
      idempotencyKey: 'put-filter-55',
      toLocationId: storeLoc,
    });

    const balColl = await collections.stockBalances(tenantUri);
    const recvBal = await balColl.findOne({ skuKey: skuId, locationId: recvLoc });
    const storeBal = await balColl.findOne({ skuKey: skuId, locationId: storeLoc });

    expect(Number(recvBal?.onHand ?? 0)).toBe(5);
    expect(Number(recvBal?.blockedQty ?? 0)).toBe(5);
    expect(Number(storeBal?.onHand ?? 0)).toBe(55);
    expect(Number(storeBal?.blockedQty ?? 0)).toBe(0);

    const expectedDoc = await (
      await collections.expectedReceipts(tenantUri)
    ).findOne({ expectedReceiptId: expected.expectedReceiptId });
    const row = (expectedDoc?.lines as Array<Record<string, unknown>>)[0];
    expect(Number(row?.qtyReceivedTotal)).toBe(60);
    expect(String(expectedDoc?.status)).toBe('partially_received');
  });

  it('denies cross-owner receipt', async () => {
    await expect(
      receiving.createExpectedReceipt(clientBCtx(), {
        ownerId: ownerA,
        warehouseId: whId,
      }),
    ).rejects.toBeInstanceOf(ForbiddenException);
  });

  it('rejected QC does not write off physical stock', async () => {
    const ctx = operatorCtx();
    const idem = 'idem-reject-only';
    const gr = await receiving.confirmGoodsReceipt(ctx, {
      idempotencyKey: idem,
      ownerId: ownerA,
      warehouseId: whId,
      receivingLocationId: recvLoc,
      lines: [{ skuId, qtyReceived: 3 }],
    });
    const lines = await receiving.listReceiptLines(ctx, gr.goodsReceiptId);
    const lineId = String((lines.items[0] as { receiptLineId: string }).receiptLineId);
    await receiving.applyQc(ctx, lineId, { status: 'rejected', note: 'Fail' });

    const balColl = await collections.stockBalances(tenantUri);
    const recvBal = await balColl.findOne({ skuKey: skuId, locationId: recvLoc, ownerId: ownerA });
    expect(Number(recvBal?.onHand ?? 0)).toBeGreaterThanOrEqual(3);
    expect(Number(recvBal?.blockedQty ?? 0)).toBeGreaterThanOrEqual(3);
  });
}, 240_000);