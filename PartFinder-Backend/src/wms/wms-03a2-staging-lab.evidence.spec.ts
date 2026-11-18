/**
 * Phase 03A.2 staging LAB evidence (MongoMemoryReplSet + in-process services).
 * NOT a named production/staging host — satisfies math, idempotency, preflight probes only.
 *
 * Run: npm test -- --testPathPatterns=03a2-staging-lab.evidence
 * Output: docs/wms-phase03/evidence/03a2-lab-acceptance-evidence.json
 */
import { mkdirSync, writeFileSync } from 'fs';
import { join } from 'path';
import { Test, type TestingModule } from '@nestjs/testing';
import { MongoMemoryReplSet } from 'mongodb-memory-server';
import { Types } from 'mongoose';
import { ForbiddenException } from '@nestjs/common';
import { TenantMongoService } from '../setup/tenant-mongo.service';
import { WmsReceivingService } from './services/wms-receiving.service';
import { WmsInventoryPostingService } from './services/wms-inventory-posting.service';
import { WmsStagingGateService } from './services/wms-staging-gate.service';
import { WmsTenantCollectionsService } from './services/wms-tenant-collections.service';
import { WmsTransactionGuardService } from './services/wms-transaction-guard.service';
import { WmsDiagnosticsService } from './services/wms-diagnostics.service';
import { WmsInventoryReadService } from './services/wms-inventory-read.service';
import type { WmsRequestContext } from './wms.types';

describe('03A.2 staging lab evidence', () => {
  let replSet: MongoMemoryReplSet;
  let moduleRef: TestingModule;
  let tenantMongo: TenantMongoService;
  let tenantUri: string;
  let receiving: WmsReceivingService;
  let collections: WmsTenantCollectionsService;
  let stagingGate: WmsStagingGateService;
  let inventoryRead: WmsInventoryReadService;
  let whId: string;
  let ownerA: string;
  let ownerB: string;
  let recvLoc: string;
  let storeLoc: string;
  let skuId: string;

  const adminCtx = (): WmsRequestContext => ({
    orgCode: '900200',
    organizationName: 'WMS Staging Lab',
    tenantUri,
    email: 'wms-admin@staging-lab.test',
    role: 'Admin',
    isWmsAdmin: true,
    allowedWarehouseIds: 'all',
    allowedOwnerIds: 'all',
    wmsEnabled: true,
    maxWmsWarehouses: 10,
    maxStockOwners: 50,
    operatorPermissions: {
      receiving: true,
      qc: true,
      putaway: true,
      supervise: true,
    },
  });

  const operatorACtx = (): WmsRequestContext => ({
    orgCode: '900200',
    organizationName: 'WMS Staging Lab',
    tenantUri,
    email: 'operator-a@staging-lab.test',
    role: 'Employee',
    isWmsAdmin: false,
    allowedWarehouseIds: [whId],
    allowedOwnerIds: [ownerA],
    operatorPermissions: { receiving: true, qc: true, putaway: true, supervise: false },
    wmsEnabled: true,
    maxWmsWarehouses: 10,
    maxStockOwners: 50,
  });

  const operatorBCtx = (): WmsRequestContext => ({
    ...operatorACtx(),
    email: 'operator-b@staging-lab.test',
    allowedOwnerIds: [ownerB],
  });

  beforeAll(async () => {
    process.env.WMS_DEPLOYMENT_TIER = 'staging';
    process.env.WMS_STAGING_STOCK_POSTING = 'true';
    process.env.WMS_LIVE_STOCK_POSTING = 'false';

    replSet = await MongoMemoryReplSet.create({ replSet: { count: 1 } });
    tenantUri = replSet.getUri('wms_staging_lab_03a2');

    moduleRef = await Test.createTestingModule({
      providers: [
        TenantMongoService,
        WmsTenantCollectionsService,
        WmsDiagnosticsService,
        WmsTransactionGuardService,
        WmsInventoryPostingService,
        WmsStagingGateService,
        WmsReceivingService,
        WmsInventoryReadService,
      ],
    }).compile();

    tenantMongo = moduleRef.get(TenantMongoService);
    collections = moduleRef.get(WmsTenantCollectionsService);
    receiving = moduleRef.get(WmsReceivingService);
    stagingGate = moduleRef.get(WmsStagingGateService);
    inventoryRead = moduleRef.get(WmsInventoryReadService);

    whId = new Types.ObjectId().toHexString();
    ownerA = new Types.ObjectId().toHexString();
    ownerB = new Types.ObjectId().toHexString();
    recvLoc = new Types.ObjectId().toHexString();
    storeLoc = new Types.ObjectId().toHexString();
    skuId = '01HWMSSTAGINGFILTER';

    const conn = await collections.connection(tenantUri);
    await conn.db.collection('wms_warehouses').insertOne({
      _id: new Types.ObjectId(whId),
      code: 'WH-STG-LAB',
      name: 'Staging Lab',
      isActive: true,
    });
    await conn.db.collection('wms_stock_owners').insertMany([
      { _id: new Types.ObjectId(ownerA), code: 'CLIENT-A', name: 'Client A', isActive: true },
      { _id: new Types.ObjectId(ownerB), code: 'CLIENT-B', name: 'Client B', isActive: true },
    ]);
    await conn.db.collection('wms_locations').insertMany([
      {
        _id: new Types.ObjectId(recvLoc),
        warehouseId: whId,
        path: 'RECV-01',
        type: 'receiving',
        isActive: true,
      },
      {
        _id: new Types.ObjectId(storeLoc),
        warehouseId: whId,
        path: 'A-01-01',
        type: 'storage',
        isActive: true,
      },
    ]);
    await conn.db.collection('wms_skus').insertOne({
      skuId,
      displayName: 'Filter',
      primaryUom: 'EA',
      status: 'active',
    });

    await conn.db.collection('wms_operator_grants').insertMany([
      {
        email: 'operator-a@staging-lab.test',
        warehouseIds: [whId],
        ownerIds: [ownerA],
        permissions: { receiving: true, qc: true, putaway: true, supervise: false },
      },
      {
        email: 'operator-b@staging-lab.test',
        warehouseIds: [whId],
        ownerIds: [ownerB],
        permissions: { receiving: true, qc: true, putaway: true, supervise: false },
      },
    ]);
  }, 180_000);

  afterAll(async () => {
    await tenantMongo?.onModuleDestroy();
    await moduleRef?.close();
    if (replSet) await replSet.stop();
    delete process.env.WMS_STAGING_STOCK_POSTING;
    delete process.env.WMS_DEPLOYMENT_TIER;
  });

  it('records lab preflight + 100-filter workflow evidence', async () => {
    const preflight = await stagingGate.recordPreflightEvidence(
      adminCtx(),
      '03A.2 staging lab evidence suite',
    );

    const expected = await receiving.createExpectedReceipt(operatorACtx(), {
      ownerId: ownerA,
      warehouseId: whId,
      externalPoId: 'PO-100-FILTERS',
    });
    await receiving.addExpectedLines(operatorACtx(), expected.expectedReceiptId, {
      lines: [{ skuId, qtyExpected: 100, primaryUom: 'EA' }],
    });

    const idem = '03a2-lab-idem-filter-100';
    const first = await receiving.confirmGoodsReceipt(operatorACtx(), {
      idempotencyKey: idem,
      ownerId: ownerA,
      warehouseId: whId,
      receivingLocationId: recvLoc,
      expectedReceiptId: expected.expectedReceiptId,
      lines: [{ skuId, qtyReceived: 60 }],
    });
    const dup = await receiving.confirmGoodsReceipt(operatorACtx(), {
      idempotencyKey: idem,
      ownerId: ownerA,
      warehouseId: whId,
      receivingLocationId: recvLoc,
      expectedReceiptId: expected.expectedReceiptId,
      lines: [{ skuId, qtyReceived: 60 }],
    });

    const lines = await receiving.listReceiptLines(operatorACtx(), first.goodsReceiptId);
    const lineId = String((lines.items[0] as { receiptLineId: string }).receiptLineId);

    await receiving.applyQc(operatorACtx(), lineId, {
      status: 'partial',
      qtyAccepted: 55,
      qtyRejected: 5,
      note: 'Lab QC',
    });

    const putaways = await receiving.listPutaway(operatorACtx(), whId);
    const taskId = String((putaways.items[0] as { putawayTaskId: string }).putawayTaskId);
    await receiving.completePutaway(operatorACtx(), taskId, {
      idempotencyKey: '03a2-lab-put-55',
      toLocationId: storeLoc,
    });

    const balColl = await collections.stockBalances(tenantUri);
    const recvBal = await balColl.findOne({
      skuKey: skuId,
      locationId: recvLoc,
      ownerId: ownerA,
    });
    const storeBal = await balColl.findOne({
      skuKey: skuId,
      locationId: storeLoc,
      ownerId: ownerA,
    });

    const expectedDoc = await (
      await collections.expectedReceipts(tenantUri)
    ).findOne({ expectedReceiptId: expected.expectedReceiptId });
    const row = (expectedDoc?.lines as Array<Record<string, unknown>>)[0];

    const txnColl = await collections.stockTransactions(tenantUri);
    const txnTypes = await txnColl.distinct('type');

    let crossOwner403 = false;
    try {
      await receiving.createExpectedReceipt(operatorBCtx(), {
        ownerId: ownerA,
        warehouseId: whId,
      });
    } catch (e) {
      crossOwner403 = e instanceof ForbiddenException;
    }

    const atp = await inventoryRead.computeAtpForSku(
      operatorACtx(),
      ownerA,
      whId,
      skuId,
    );

    const whOnHand =
      Number(recvBal?.onHand ?? 0) + Number(storeBal?.onHand ?? 0);
    const blocked = Number(recvBal?.blockedQty ?? 0);
    const storageOnHand = Number(storeBal?.onHand ?? 0);

    const evidence = {
      classification: 'STAGING_LAB_MongoMemoryReplSet',
      notNamedHostedStaging: true,
      winUiExecuted: false,
      executedAt: new Date().toISOString(),
      productionPostingEnabled: false,
      deploymentTier: process.env.WMS_DEPLOYMENT_TIER,
      preflight: {
        orgCode: preflight.orgCode,
        tenantDatabase: preflight.tenantDatabase,
        replicaSetName: preflight.replicaSetName,
        commitProbe: preflight.commitProbe,
        rollbackProbe: preflight.rollbackProbe,
        supported: preflight.supported,
        deploymentTier: (preflight as { deploymentTier?: string }).deploymentTier,
        stagingStockPosting: preflight.stagingStockPosting,
        liveStockPosting: preflight.liveStockPosting,
        postingGateOpen: preflight.postingGateOpen,
      },
      scenario: {
        qtyExpected: 100,
        qtyReceived: 60,
        qtyAccepted: 55,
        qtyRejected: 5,
        expectedRemaining: Number(row?.qtyExpected) - Number(row?.qtyReceivedTotal),
        expectedStatus: expectedDoc?.status,
      },
      reconciliation: {
        warehouseOnHand: whOnHand,
        blockedAtReceiving: blocked,
        availableAtStorage: storageOnHand,
        atpFromService: atp,
      },
      idempotency: { key: idem, firstDuplicate: first.duplicate, secondDuplicate: dup.duplicate },
      authorization: { operatorBCrossOwnerA: crossOwner403 ? '403' : 'fail' },
      ledgerTransactionTypes: txnTypes.sort(),
      productionTierStagingFlagIgnored: (() => {
        process.env.WMS_DEPLOYMENT_TIER = 'production';
        process.env.WMS_STAGING_STOCK_POSTING = 'true';
        const {
          isWmsStagingStockPostingEnabled,
        } = require('./wms-staging-stock.util');
        const ignored = isWmsStagingStockPostingEnabled() === false;
        process.env.WMS_DEPLOYMENT_TIER = 'staging';
        return ignored;
      })(),
    };

    expect(preflight.commitProbe).toBe(true);
    expect(preflight.rollbackProbe).toBe(true);
    expect(first.duplicate).toBe(false);
    expect(dup.duplicate).toBe(true);
    expect(crossOwner403).toBe(true);
    expect(whOnHand).toBe(60);
    expect(blocked).toBe(5);
    expect(storageOnHand).toBe(55);
    expect(Number(row?.qtyReceivedTotal)).toBe(60);
    expect(String(expectedDoc?.status)).toBe('partially_received');
    expect(evidence.scenario.expectedRemaining).toBe(40);

    const outDir = join(__dirname, '..', '..', '..', 'docs', 'wms-phase03', 'evidence');
    mkdirSync(outDir, { recursive: true });
    const outPath = join(outDir, '03a2-lab-acceptance-evidence.json');
    writeFileSync(outPath, JSON.stringify(evidence, null, 2), 'utf8');
  }, 120_000);
}, 240_000);