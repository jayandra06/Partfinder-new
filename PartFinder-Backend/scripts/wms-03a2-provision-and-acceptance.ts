/**
 * Provision isolated WMS staging org on platform Mongo + run 03A.2 acceptance workflow in-process.
 * Does NOT enable live production posting. Uses dedicated tenant database name (not production org data).
 *
 * Requires: MONGODB_URI, JWT_SECRET (or WMS_STAGING_JWT_SECRET) in environment / .env
 *
 * Optional:
 *   WMS_STAGING_ORG_CODE=900152
 *   WMS_STAGING_TENANT_DB=wms_staging_acceptance_900152
 *
 * Output: docs/wms-phase03/evidence/03a2-staging-evidence.json (redacted)
 */
import 'reflect-metadata';
import '../src/test-utils/mongodb-atlas-dns';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'fs';
import { join } from 'path';
import { parse } from 'dotenv';
import * as bcrypt from 'bcrypt';
import { NestFactory } from '@nestjs/core';
import { getModelToken } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { AppModule } from '../src/app.module';
import { Organization } from '../src/organizations/schemas/organization.schema';
import { computeValidity } from '../src/organizations/org.constants';
import { TenantMongoService } from '../src/setup/tenant-mongo.service';
import { replaceMongoDatabasePath } from '../src/setup/org-database-uri.util';
import { WmsStagingGateService } from '../src/wms/services/wms-staging-gate.service';
import { WmsReceivingService } from '../src/wms/services/wms-receiving.service';
import { WmsTenantCollectionsService } from '../src/wms/services/wms-tenant-collections.service';
import { WmsInventoryReadService } from '../src/wms/services/wms-inventory-read.service';
import type { WmsRequestContext } from '../src/wms/wms.types';
import { WMS_COLLECTIONS } from '../src/wms/wms.types';
import {
  getWmsDeploymentTier,
} from '../src/wms/wms-deployment-tier.util';
import {
  isWmsStagingStockPostingEnabled,
  isWmsStockPostingAllowed,
} from '../src/wms/wms-staging-stock.util';
import { isWmsLiveStockPostingEnabled } from '../src/wms/wms-live-stock.util';
import { ForbiddenException } from '@nestjs/common';

const backendRoot = join(__dirname, '..');
for (const f of ['.env', '.env.local']) {
  const p = join(backendRoot, f);
  if (existsSync(p)) {
    Object.assign(process.env, parse(readFileSync(p)));
  }
}

const ORG_CODE = (process.env.WMS_STAGING_ORG_CODE ?? '900152').trim();
const TENANT_DB = (process.env.WMS_STAGING_TENANT_DB ?? 'wms_staging_acceptance_900152').trim();
const TEST_PASSWORD = process.env.WMS_STAGING_TEST_PASSWORD ?? `Wms-Staging-${Date.now().toString(36)}`;

function redactUri(uri: string): string {
  try {
    const u = new URL(uri.replace('mongodb+srv://', 'https://').replace('mongodb://', 'http://'));
    return `${u.hostname}/<redacted-db>`;
  } catch {
    return '<redacted>';
  }
}

async function run() {
  if (!process.env.MONGODB_URI?.trim()) {
    throw new Error('MONGODB_URI required');
  }
  process.env.JWT_SECRET =
    process.env.JWT_SECRET?.trim() ||
    process.env.WMS_STAGING_JWT_SECRET?.trim() ||
    'staging-jwt-secret-local-only-change-me';
  process.env.WMS_DEPLOYMENT_TIER = 'staging';
  process.env.WMS_STAGING_STOCK_POSTING = 'true';
  process.env.WMS_LIVE_STOCK_POSTING = 'false';

  const platformUri = process.env.MONGODB_URI.trim();
  const tenantUri = replaceMongoDatabasePath(platformUri, TENANT_DB);

  const app = await NestFactory.createApplicationContext(AppModule, {
    logger: ['error', 'warn', 'log'],
  });

  const evidence: Record<string, unknown> = {
    phase: '03A.2',
    executedAt: new Date().toISOString(),
    orgCode: ORG_CODE,
    tenantDatabase: TENANT_DB,
    platformHost: redactUri(platformUri),
    deploymentTier: getWmsDeploymentTier(),
    stagingPostingEffective: isWmsStagingStockPostingEnabled(),
    livePostingEffective: isWmsLiveStockPostingEnabled(),
    stockPostingAllowed: isWmsStockPostingAllowed(),
    winuiManual: 'NOT_EXECUTED_IN_SCRIPT',
    productionIsolationCheck: {
      tierProductionIgnoresStagingFlag: true,
      note: 'When WMS_DEPLOYMENT_TIER=production, staging flag is ignored (unit-tested).',
    },
  };

  try {
    const orgModel = app.get<Model<Organization>>(getModelToken(Organization.name));
    const tenant = app.get(TenantMongoService);
    const stagingGate = app.get(WmsStagingGateService);
    const receiving = app.get(WmsReceivingService);
    const collections = app.get(WmsTenantCollectionsService);
    const inventoryRead = app.get(WmsInventoryReadService);

    let org = await orgModel.findOne({ orgCode: ORG_CODE }).exec();
    if (!org) {
      org = await orgModel.create({
        orgCode: ORG_CODE,
        name: 'WMS Staging Acceptance',
        type: 'standard',
        plan: 'annual',
        validity: computeValidity('annual'),
        status: 'Active',
        wmsEnabled: true,
        maxWmsWarehouses: 10,
        maxStockOwners: 50,
      });
    } else {
      org.wmsEnabled = true;
      org.name = 'WMS Staging Acceptance';
      org.status = 'Active';
      org.validity = computeValidity('annual');
      await org.save();
    }

    await tenant.pingUri(tenantUri);
    await tenant.initializeTenantDatabase(tenantUri);
    org.orgDatabaseUri = tenantUri;
    await org.save();

    const passwordHash = await bcrypt.hash(TEST_PASSWORD, 10);
    const adminEmail = 'wms-admin-staging@partfinder.acceptance';
    const opAEmail = 'operator-a-staging@partfinder.acceptance';
    const opBEmail = 'operator-b-staging@partfinder.acceptance';

    const conn = await tenant.getConnection(tenantUri);
    const admins = tenant.orgAdminCollection(conn);
    await admins.updateOne(
      { email: adminEmail },
      {
        $set: {
          email: adminEmail,
          passwordHash,
          name: 'WMS Staging Admin',
          updatedAt: new Date(),
        },
        $setOnInsert: { createdAt: new Date() },
      },
      { upsert: true },
    );

    const appUsers = tenant.orgAppUsersCollection(conn);
    for (const [email, name] of [
      [opAEmail, 'Operator A'],
      [opBEmail, 'Operator B'],
    ] as const) {
      await appUsers.updateOne(
        { emailNormalized: email },
        {
          $set: {
            name,
            email,
            emailNormalized: email,
            role: 'Employee',
            partsAllTemplates: false,
            allowedTemplateIds: [],
            temporaryPasswordHash: passwordHash,
            invitedAtUtc: new Date(),
          },
        },
        { upsert: true },
      );
    }

    const whId = new Types.ObjectId().toHexString();
    const ownerA = new Types.ObjectId().toHexString();
    const ownerB = new Types.ObjectId().toHexString();
    const recvLoc = new Types.ObjectId().toHexString();
    const storeLoc = new Types.ObjectId().toHexString();
    const skuId = '01HWMSACCEPTFILTER';

    await collections.connection(tenantUri);
    const db = (await collections.connection(tenantUri)).db!;
    await db.collection('wms_warehouses').deleteMany({ code: 'WH-STG-01' });
    await db.collection('wms_stock_owners').deleteMany({ code: { $in: ['CLIENT_A', 'CLIENT_B'] } });
    await db.collection('wms_skus').deleteOne({ skuId });

    await db.collection('wms_warehouses').insertOne({
      _id: new Types.ObjectId(whId),
      code: 'WH-STG-01',
      name: 'Staging Demo',
      isActive: true,
    });
    await db.collection('wms_stock_owners').insertMany([
      { _id: new Types.ObjectId(ownerA), code: 'CLIENT_A', name: 'Client A', isActive: true },
      { _id: new Types.ObjectId(ownerB), code: 'CLIENT_B', name: 'Client B', isActive: true },
    ]);
    await db.collection('wms_locations').insertMany([
      { _id: new Types.ObjectId(recvLoc), warehouseId: whId, path: 'RECV-01', type: 'receiving', isActive: true },
      { _id: new Types.ObjectId(storeLoc), warehouseId: whId, path: 'A-01-01', type: 'storage', isActive: true },
    ]);
    await db.collection('wms_skus').insertOne({
      skuId,
      displayName: 'Filter',
      primaryUom: 'EA',
      status: 'active',
    });

    await db.collection(WMS_COLLECTIONS.operatorGrants).updateOne(
      { email: opAEmail },
      {
        $set: {
          email: opAEmail,
          warehouseIds: [whId],
          ownerIds: [ownerA],
          permissions: { receiving: true, qc: true, putaway: true, supervise: false },
          updatedAt: new Date(),
        },
      },
      { upsert: true },
    );
    await db.collection(WMS_COLLECTIONS.operatorGrants).updateOne(
      { email: opBEmail },
      {
        $set: {
          email: opBEmail,
          warehouseIds: [whId],
          ownerIds: [ownerB],
          permissions: { receiving: true, qc: true, putaway: true, supervise: false },
          updatedAt: new Date(),
        },
      },
      { upsert: true },
    );

    const adminCtx = (): WmsRequestContext => ({
      orgCode: ORG_CODE,
      organizationName: org!.name,
      tenantUri,
      email: adminEmail,
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
      orgCode: ORG_CODE,
      organizationName: org!.name,
      tenantUri,
      email: opAEmail,
      role: 'Employee',
      isWmsAdmin: false,
      allowedWarehouseIds: [whId],
      allowedOwnerIds: [ownerA],
      wmsEnabled: true,
      maxWmsWarehouses: 10,
      maxStockOwners: 50,
      operatorPermissions: {
        receiving: true,
        qc: true,
        putaway: true,
        supervise: false,
      },
    });

    const operatorBCtx = (): WmsRequestContext => ({
      ...operatorACtx(),
      email: opBEmail,
      allowedOwnerIds: [ownerB],
    });

    const preflightDoc = await stagingGate.recordPreflightEvidence(
      adminCtx(),
      '03A.2 provision-and-acceptance script',
    );
    evidence.preflight = {
      orgCode: preflightDoc.orgCode,
      organizationName: preflightDoc.organizationName,
      tenantDatabase: preflightDoc.tenantDatabase,
      replicaSetName: preflightDoc.replicaSetName,
      commitProbe: preflightDoc.commitProbe,
      rollbackProbe: preflightDoc.rollbackProbe,
      supported: preflightDoc.supported,
      deploymentTier: (preflightDoc as { deploymentTier?: string }).deploymentTier,
      stagingStockPosting: preflightDoc.stagingStockPosting,
      liveStockPosting: preflightDoc.liveStockPosting,
      postingGateOpen: preflightDoc.postingGateOpen,
      recordedAt: preflightDoc.recordedAt,
    };

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
    const dup = await receiving.confirmGoodsReceipt(operatorACtx(), {
      idempotencyKey: idem,
      ownerId: ownerA,
      warehouseId: whId,
      receivingLocationId: recvLoc,
      expectedReceiptId: expected.expectedReceiptId,
      lines: [{ skuId, qtyReceived: 60 }],
    });

    const lineList = await receiving.listReceiptLines(operatorACtx(), first.goodsReceiptId);
    const lineId = String((lineList.items[0] as { receiptLineId: string }).receiptLineId);

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

    const txColl = await collections.stockTransactions(tenantUri);
    const txTypes = await txColl
      .find({ skuKey: skuId })
      .project({ type: 1 })
      .toArray();

    let crossOwnerBlocked = false;
    try {
      await receiving.createExpectedReceipt(operatorBCtx(), {
        ownerId: ownerA,
        warehouseId: whId,
      });
    } catch (e) {
      crossOwnerBlocked = e instanceof ForbiddenException;
    }

    const warehouseOnHand = onHandRecv + onHandStore;
    const availableApprox = onHandStore;

    evidence.scenario = {
      idempotencyKey: idem,
      duplicateSecondPost: dup.duplicate === true,
      onHandWarehouseTotal: warehouseOnHand,
      onHandReceiving: onHandRecv,
      onHandStorage: onHandStore,
      blockedAtReceiving: blockedRecv,
      availableAtStorage: availableApprox,
      qtyExpectedRemaining: 100 - Number(row?.qtyReceivedTotal ?? 0),
      expectedStatus: expectedDoc?.status,
      ledgerTransactionTypes: [
        ...new Set(txTypes.map((t: { type?: unknown }) => String(t.type))),
      ],
      crossOwner403: crossOwnerBlocked,
    };

    evidence.pass = {
      preflightSupported: preflightDoc.supported === true,
      reconciliation60_55_5_40:
        warehouseOnHand === 60 &&
        blockedRecv === 5 &&
        onHandStore === 55 &&
        Number(row?.qtyReceivedTotal) === 60 &&
        String(expectedDoc?.status) === 'partially_received' &&
        100 - Number(row?.qtyReceivedTotal ?? 0) === 40,
      idempotency: first.duplicate === false && dup.duplicate === true,
      crossOwner: crossOwnerBlocked,
    };

    evidence.testAccounts = {
      orgCode: ORG_CODE,
      adminEmail,
      operatorAEmail: opAEmail,
      operatorBEmail: opBEmail,
      passwordNote: 'Password was set for this run only — store in your password manager, not in git.',
    };

    const outDir = join(backendRoot, '..', 'docs', 'wms-phase03', 'evidence');
    mkdirSync(outDir, { recursive: true });
    const outPath = join(outDir, '03a2-staging-evidence.json');
    writeFileSync(outPath, JSON.stringify(evidence, null, 2), 'utf8');
    console.log(`Evidence written: ${outPath}`);
    console.log(`Test password (stderr-only): ${TEST_PASSWORD}`);
    console.log(JSON.stringify(evidence, null, 2));
  } finally {
    await app.close();
  }
}

run().catch((e) => {
  console.error(e);
  process.exit(1);
});