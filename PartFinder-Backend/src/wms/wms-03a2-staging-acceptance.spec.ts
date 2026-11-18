/**
 * Named staging acceptance on isolated tenant DB (Atlas/platform URI).
 * Skipped unless WMS_RUN_STAGING_ACCEPTANCE=true and MONGODB_URI is set.
 * NOT a substitute for WinUI manual sign-off — records API/ledger evidence only.
 */
import '../test-utils/mongodb-atlas-dns';
import { existsSync, readFileSync } from 'fs';
import { join } from 'path';
import { parse } from 'dotenv';

const backendRoot = join(__dirname, '..', '..');
for (const f of ['.env', '.env.local']) {
  const p = join(backendRoot, f);
  if (existsSync(p)) {
    Object.assign(process.env, parse(readFileSync(p)));
  }
}

import { afterAll, beforeAll, describe, expect, it } from '@jest/globals';
import { ForbiddenException } from '@nestjs/common';
import { Test, type TestingModule } from '@nestjs/testing';
import { MongoClient } from 'mongodb';
import { Types } from 'mongoose';
import { replaceMongoDatabasePath } from '../setup/org-database-uri.util';
import { TenantMongoService } from '../setup/tenant-mongo.service';
import { WmsDiagnosticsService } from './services/wms-diagnostics.service';
import { WmsInventoryPostingService } from './services/wms-inventory-posting.service';
import { WmsReceivingService } from './services/wms-receiving.service';
import { WmsStagingGateService } from './services/wms-staging-gate.service';
import { WmsTenantCollectionsService } from './services/wms-tenant-collections.service';
import { WmsTransactionGuardService } from './services/wms-transaction-guard.service';
import type { WmsRequestContext } from './wms.types';
import { WMS_COLLECTIONS } from './wms.types';
import { mkdirSync, writeFileSync } from 'fs';

const RUN =
  process.env.WMS_RUN_STAGING_ACCEPTANCE === 'true' &&
  Boolean(process.env.MONGODB_URI?.trim());

const ORG_CODE = (process.env.WMS_STAGING_ORG_CODE ?? '900152').trim();
const TENANT_DB = (process.env.WMS_STAGING_TENANT_DB ?? 'wms_staging_acceptance_900152').trim();

(RUN ? describe : describe.skip)('Phase 03A.2 staging acceptance (isolated tenant)', () => {
  let platformUri: string;
  let tenantUri: string;
  let moduleRef: TestingModule;
  let tenantMongo: TenantMongoService;
  let collections: WmsTenantCollectionsService;
  let stagingGate: WmsStagingGateService;
  let receiving: WmsReceivingService;
  let whId: string;
  let ownerA: string;
  let ownerB: string;
  let recvLoc: string;
  let storeLoc: string;
  let skuId: string;
  let evidence: Record<string, unknown>;

  const operatorACtx = (): WmsRequestContext => ({
    orgCode: ORG_CODE,
    organizationName: 'WMS Staging Acceptance',
    tenantUri,
    email: 'operator-a-staging@partfinder.acceptance',
    role: 'Employee',
    isWmsAdmin: false,
    allowedWarehouseIds: [whId],
    allowedOwnerIds: [ownerA],
    wmsEnabled: true,
    maxWmsWarehouses: 10,
    maxStockOwners: 50,
    operatorPermissions: { receiving: true, qc: true, putaway: true, supervise: false },
  });

  const operatorBCtx = (): WmsRequestContext => ({
    ...operatorACtx(),
    email: 'operator-b-staging@partfinder.acceptance',
    allowedOwnerIds: [ownerB],
  });

  const adminCtx = (): WmsRequestContext => ({
    orgCode: ORG_CODE,
    organizationName: 'WMS Staging Acceptance',
    tenantUri,
    email: 'wms-admin-staging@partfinder.acceptance',
    role: 'Admin',
    isWmsAdmin: true,
    allowedWarehouseIds: 'all',
    allowedOwnerIds: 'all',
    wmsEnabled: true,
    maxWmsWarehouses: 10,
    maxStockOwners: 50,
    operatorPermissions: { receiving: true, qc: true, putaway: true, supervise: true },
  });

  beforeAll(async () => {
    process.env.WMS_DEPLOYMENT_TIER = 'staging';
    process.env.WMS_STAGING_STOCK_POSTING = 'true';
    process.env.WMS_LIVE_STOCK_POSTING = 'false';

    platformUri = process.env.MONGODB_URI!.trim();
    tenantUri = replaceMongoDatabasePath(platformUri, TENANT_DB);

    const client = new MongoClient(platformUri);
    await client.connect();
    const db = client.db();
    await db.collection('organizations').updateOne(
      { orgCode: ORG_CODE },
      {
        $set: {
          orgCode: ORG_CODE,
          name: 'WMS Staging Acceptance',
          wmsEnabled: true,
          status: 'Active',
          orgDatabaseUri: tenantUri,
          maxWmsWarehouses: 10,
          maxStockOwners: 50,
          updatedAt: new Date(),
        },
        $setOnInsert: { createdAt: new Date(), type: 'standard', plan: 'annual' },
      },
      { upsert: true },
    );
    await client.close();

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
    stagingGate = moduleRef.get(WmsStagingGateService);
    receiving = moduleRef.get(WmsReceivingService);

    await tenantMongo.pingUri(tenantUri);
    await tenantMongo.initializeTenantDatabase(tenantUri);

    skuId = '01HWMSACCEPTFILTER';

    const whColl = await collections.warehouses(tenantUri);
    const ownerColl = await collections.stockOwners(tenantUri);
    const locColl = await collections.locations(tenantUri);
    const skuColl = await collections.skus(tenantUri);
    const conn = await collections.connection(tenantUri);
    const tenantDb = conn.db;
    if (!tenantDb) {
      throw new Error('Tenant Mongo connection has no database handle');
    }
    const grantsColl = tenantDb.collection(WMS_COLLECTIONS.operatorGrants);

    const existingWh = await whColl.findOne({ code: 'WH-STG-01' });
    if (existingWh?._id) {
      whId = String(existingWh._id);
    } else {
      whId = new Types.ObjectId().toHexString();
      await whColl.insertOne({
        _id: new Types.ObjectId(whId),
        code: 'WH-STG-01',
        name: 'Staging Demo',
        isActive: true,
      });
    }

    const resolveOwner = async (code: string, name: string): Promise<string> => {
      const existing = await ownerColl.findOne({ code });
      if (existing?._id) {
        return String(existing._id);
      }
      const id = new Types.ObjectId().toHexString();
      await ownerColl.insertOne({
        _id: new Types.ObjectId(id),
        code,
        name,
        isActive: true,
      });
      return id;
    };
    ownerA = await resolveOwner('CLIENT_A', 'Client A');
    ownerB = await resolveOwner('CLIENT_B', 'Client B');

    const resolveLocation = async (
      path: string,
      type: string,
    ): Promise<string> => {
      const existing = await locColl.findOne({ warehouseId: whId, path });
      if (existing?._id) {
        return String(existing._id);
      }
      const id = new Types.ObjectId().toHexString();
      await locColl.insertOne({
        _id: new Types.ObjectId(id),
        warehouseId: whId,
        path,
        type,
        isActive: true,
      });
      return id;
    };
    recvLoc = await resolveLocation('RECV-01', 'receiving');
    storeLoc = await resolveLocation('A-01-01', 'storage');

    await skuColl.updateOne(
      { skuId },
      {
        $set: {
          skuId,
          displayName: 'Filter',
          primaryUom: 'EA',
          status: 'active',
        },
      },
      { upsert: true },
    );

    const upsertGrant = async (
      email: string,
      ownerIds: string[],
    ): Promise<void> => {
      await grantsColl.updateOne(
        { email },
        {
          $set: {
            email,
            warehouseIds: [whId],
            ownerIds,
            permissions: { receiving: true, qc: true, putaway: true, supervise: false },
          },
        },
        { upsert: true },
      );
    };
    await upsertGrant('operator-a-staging@partfinder.acceptance', [ownerA]);
    await upsertGrant('operator-b-staging@partfinder.acceptance', [ownerB]);

    evidence = {
      phase: '03A.2',
      executedAt: new Date().toISOString(),
      orgCode: ORG_CODE,
      tenantDatabase: TENANT_DB,
      winuiManual: 'NOT_EXECUTED',
    };
  }, 300_000);

  afterAll(async () => {
    await tenantMongo?.onModuleDestroy();
    await moduleRef?.close();
    delete process.env.WMS_STAGING_STOCK_POSTING;
  });

  /** Idempotent re-runs: clear prior receipt/posting state for this warehouse (master data kept). */
  async function resetWarehouseAcceptanceState(): Promise<void> {
    const whFilter = { warehouseId: whId };
    await (
      await collections.stockBalances(tenantUri)
    ).deleteMany({
      skuKey: skuId,
      locationId: { $in: [recvLoc, storeLoc] },
    });
    await (await collections.stockTransactions(tenantUri)).deleteMany({
      warehouseId: whId,
      skuKey: skuId,
    });
    await (await collections.expectedReceipts(tenantUri)).deleteMany(whFilter);
    await (await collections.goodsReceipts(tenantUri)).deleteMany(whFilter);
    await (await collections.receiptLines(tenantUri)).deleteMany(whFilter);
    await (await collections.qcInspections(tenantUri)).deleteMany(whFilter);
    await (await collections.putawayTasks(tenantUri)).deleteMany(whFilter);
    await (await collections.receivingAuditLog(tenantUri)).deleteMany(whFilter);
    await (await collections.idempotencyKeys(tenantUri)).deleteMany({});
  }

  it('records preflight and completes 100-filter workflow', async () => {
    await resetWarehouseAcceptanceState();

    const preflightDoc = await stagingGate.recordPreflightEvidence(
      adminCtx(),
      '03A.2 staging acceptance spec',
    );
    evidence.preflight = {
      orgCode: preflightDoc.orgCode,
      tenantDatabase: preflightDoc.tenantDatabase,
      replicaSetName: preflightDoc.replicaSetName,
      supported: preflightDoc.supported,
      commitProbe: preflightDoc.commitProbe,
      rollbackProbe: preflightDoc.rollbackProbe,
      deploymentTier: (preflightDoc as { deploymentTier?: string }).deploymentTier,
      stagingStockPosting: preflightDoc.stagingStockPosting,
      liveStockPosting: preflightDoc.liveStockPosting,
      postingGateOpen: preflightDoc.postingGateOpen,
    };
    expect(preflightDoc.supported).toBe(true);
    expect(preflightDoc.commitProbe).toBe(true);
    expect(preflightDoc.rollbackProbe).toBe(true);

    const expected = await receiving.createExpectedReceipt(operatorACtx(), {
      ownerId: ownerA,
      warehouseId: whId,
      externalPoId: 'PO-100-FILTERS',
    });
    await receiving.addExpectedLines(operatorACtx(), expected.expectedReceiptId, {
      lines: [{ skuId, qtyExpected: 100, primaryUom: 'EA' }],
    });

    const idem = `03a2-staging-${ORG_CODE}-${Date.now()}`;
    const first = await receiving.confirmGoodsReceipt(operatorACtx(), {
      idempotencyKey: idem,
      ownerId: ownerA,
      warehouseId: whId,
      receivingLocationId: recvLoc,
      expectedReceiptId: expected.expectedReceiptId,
      lines: [{ skuId, qtyReceived: 60 }],
    });
    expect(first.duplicate).toBe(false);

    const dup = await receiving.confirmGoodsReceipt(operatorACtx(), {
      idempotencyKey: idem,
      ownerId: ownerA,
      warehouseId: whId,
      receivingLocationId: recvLoc,
      expectedReceiptId: expected.expectedReceiptId,
      lines: [{ skuId, qtyReceived: 60 }],
    });
    expect(dup.duplicate).toBe(true);

    const lines = await receiving.listReceiptLines(operatorACtx(), first.goodsReceiptId);
    const lineId = String((lines.items[0] as { receiptLineId: string }).receiptLineId);

    await receiving.applyQc(operatorACtx(), lineId, {
      status: 'partial',
      qtyAccepted: 55,
      qtyRejected: 5,
      note: '03A.2 acceptance',
    });

    const putaways = await receiving.listPutaway(operatorACtx(), whId);
    const taskId = String((putaways.items[0] as { putawayTaskId: string }).putawayTaskId);
    await receiving.completePutaway(operatorACtx(), taskId, {
      idempotencyKey: `put-${idem}`,
      toLocationId: storeLoc,
    });

    const balColl = await collections.stockBalances(tenantUri);
    const recvBal = await balColl.findOne({ skuKey: skuId, locationId: recvLoc });
    const storeBal = await balColl.findOne({ skuKey: skuId, locationId: storeLoc });

    const onHandRecv = Number(recvBal?.onHand ?? 0);
    const blockedRecv = Number(recvBal?.blockedQty ?? 0);
    const onHandStore = Number(storeBal?.onHand ?? 0);

    const expectedDoc = await (
      await collections.expectedReceipts(tenantUri)
    ).findOne({ expectedReceiptId: expected.expectedReceiptId });
    const row = (expectedDoc?.lines as Array<Record<string, unknown>>)[0];

    let crossOwnerBlocked = false;
    try {
      await receiving.createExpectedReceipt(operatorBCtx(), {
        ownerId: ownerA,
        warehouseId: whId,
      });
    } catch (e) {
      crossOwnerBlocked = e instanceof ForbiddenException;
    }
    expect(crossOwnerBlocked).toBe(true);

    evidence.scenario = {
      idempotencyKey: idem,
      duplicateSecondPost: dup.duplicate,
      onHandWarehouseTotal: onHandRecv + onHandStore,
      onHandReceiving: onHandRecv,
      onHandStorage: onHandStore,
      blockedAtReceiving: blockedRecv,
      qtyExpectedRemaining: 100 - Number(row?.qtyReceivedTotal ?? 0),
      expectedStatus: expectedDoc?.status,
      crossOwner403: crossOwnerBlocked,
    };

    expect(onHandRecv + onHandStore).toBe(60);
    expect(blockedRecv).toBe(5);
    expect(onHandStore).toBe(55);
    expect(Number(row?.qtyReceivedTotal)).toBe(60);
    expect(String(expectedDoc?.status)).toBe('partially_received');
    expect(100 - Number(row?.qtyReceivedTotal ?? 0)).toBe(40);

    const outDir = join(__dirname, '..', '..', '..', 'docs', 'wms-phase03', 'evidence');
    mkdirSync(outDir, { recursive: true });
    writeFileSync(
      join(outDir, '03a2-staging-evidence.json'),
      JSON.stringify(evidence, null, 2),
      'utf8',
    );
  }, 300_000);
});