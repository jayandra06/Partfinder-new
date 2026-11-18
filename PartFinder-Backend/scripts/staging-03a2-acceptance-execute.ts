/**
 * Executes Phase 03A.2 acceptance on a **named** staging org (platform Mongo + tenant URI).
 * Does not enable live posting. Requires env (no secrets in repo):
 *
 *   STAGING_ORG_CODE=900xxx (6 digits)
 *   Optional: STAGING_ACCEPTANCE_ORG_ADMIN_EMAIL=... (defaults to tenant org admin)
 *
 * Sets process.env.WMS_DEPLOYMENT_TIER=staging and WMS_STAGING_STOCK_POSTING=true for this process only.
 *
 * Output: JSON summary to stdout + path from STAGING_ACCEPTANCE_REPORT_JSON (default docs path)
 */
import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { getModelToken } from '@nestjs/mongoose';
import { ForbiddenException } from '@nestjs/common';
import type { Model } from 'mongoose';
import { writeFileSync } from 'fs';
import { resolve } from 'path';
import { AppModule } from '../src/app.module';
import { Organization } from '../src/organizations/schemas/organization.schema';
import { OrganizationsService } from '../src/organizations/organizations.service';
import { TenantMongoService } from '../src/setup/tenant-mongo.service';
import { WmsStagingGateService } from '../src/wms/services/wms-staging-gate.service';
import { WmsReceivingService } from '../src/wms/services/wms-receiving.service';
import { WmsTenantCollectionsService } from '../src/wms/services/wms-tenant-collections.service';
import { WmsOperatorGrantsService } from '../src/wms/services/wms-operator-grants.service';
import type { WmsRequestContext } from '../src/wms/wms.types';
import { WmsSessionGuard } from '../src/wms/guards/wms-session.guard';
import { Types } from 'mongoose';

type StepResult = { step: string; pass: boolean; detail: string };

const FILTER_SKU = 'WMS-ACCEPT-FILTER-001';
const IDEM_KEY = 'acceptance-03a2-filter-100-v1';

async function buildCtxForEmail(
  guard: WmsSessionGuard,
  req: { user: { email: string; orgCode: string; userId: string } },
): Promise<WmsRequestContext> {
  const fakeContext = {
    switchToHttp: () => ({
      getRequest: () => ({
        user: req.user,
        headers: { 'x-org-id': req.user.orgCode },
      }),
    }),
  };
  await guard.canActivate(fakeContext as never);
  const r = fakeContext.switchToHttp().getRequest() as { wmsContext?: WmsRequestContext };
  if (!r.wmsContext) throw new Error('WMS context not built');
  return r.wmsContext;
}

async function run() {
  process.env.WMS_DEPLOYMENT_TIER = 'staging';
  process.env.WMS_STAGING_STOCK_POSTING = 'true';
  process.env.WMS_LIVE_STOCK_POSTING = 'false';

  const orgCode = (process.env.STAGING_ORG_CODE ?? '').trim();
  if (!/^\d{6}$/.test(orgCode)) {
    console.error('STAGING_ORG_CODE must be set to a 6-digit org code.');
    process.exit(2);
  }

  const app = await NestFactory.createApplicationContext(AppModule, {
    logger: ['error', 'warn', 'log'],
  });

  const steps: StepResult[] = [];
  let preflightDoc: Record<string, unknown> | null = null;

  try {
    const orgModel = app.get<Model<Organization>>(getModelToken(Organization.name));
    const orgs = app.get(OrganizationsService);
    const stagingGate = app.get(WmsStagingGateService);
    const receiving = app.get(WmsReceivingService);
    const collections = app.get(WmsTenantCollectionsService);
    const grants = app.get(WmsOperatorGrantsService);
    const guard = app.get(WmsSessionGuard);
    const tenantMongo = app.get(TenantMongoService);

    const org = await orgModel.findOne({ orgCode }).exec();
    if (!org?.orgDatabaseUri?.trim()) {
      steps.push({
        step: 'org_ready',
        pass: false,
        detail: 'Organization missing or orgDatabaseUri not configured.',
      });
      throw new Error('org not ready');
    }
    if (!org.wmsEnabled) {
      org.wmsEnabled = true;
      await org.save();
      steps.push({ step: 'wms_entitlement', pass: true, detail: 'wmsEnabled set true on staging org.' });
    } else {
      steps.push({ step: 'wms_entitlement', pass: true, detail: 'wmsEnabled already true.' });
    }

    const license = await orgs.verifyLicense(orgCode);
    if (!license.valid) {
      steps.push({ step: 'license', pass: false, detail: license.message ?? 'invalid' });
      throw new Error('license invalid');
    }
    steps.push({ step: 'license', pass: true, detail: 'License valid.' });

    const tenantUri = org.orgDatabaseUri.trim();
    const adminFromEnv = (process.env.STAGING_ACCEPTANCE_ORG_ADMIN_EMAIL ?? '').trim();
    let adminEmailResolved = adminFromEnv;
    if (!adminEmailResolved) {
      const c = await tenantMongo.getConnection(tenantUri);
      const adminDoc = await tenantMongo.orgAdminCollection(c).findOne({});
      adminEmailResolved = adminDoc?.email ? String(adminDoc.email) : undefined;
    }
    if (!adminEmailResolved) {
      steps.push({ step: 'admin_user', pass: false, detail: 'No org admin email found in tenant.' });
      throw new Error('no admin');
    }

    const adminCtx = await buildCtxForEmail(guard, {
      user: { email: adminEmailResolved.toLowerCase(), orgCode, userId: 'acceptance-admin' },
    });

    preflightDoc = (await stagingGate.recordPreflightEvidence(
      adminCtx,
      '03A.2 automated acceptance runner',
    )) as Record<string, unknown>;
    steps.push({
      step: 'preflight',
      pass: preflightDoc.supported === true && preflightDoc.commitProbe === true,
      detail: JSON.stringify({
        supported: preflightDoc.supported,
        commitProbe: preflightDoc.commitProbe,
        rollbackProbe: preflightDoc.rollbackProbe,
        tenantDatabase: preflightDoc.tenantDatabase,
        replicaSetName: preflightDoc.replicaSetName,
        deploymentTier: preflightDoc.deploymentTier,
        stagingStockPosting: preflightDoc.stagingStockPosting,
        postingGateOpen: preflightDoc.postingGateOpen,
      }),
    });

    // Ensure master data for scenario
    const whColl = await collections.warehouses(org.orgDatabaseUri.trim());
    let wh = await whColl.findOne({ code: 'WH-ACCEPT-01' });
    if (!wh) {
      const ins = await whColl.insertOne({
        code: 'WH-ACCEPT-01',
        name: 'Acceptance WH',
        timeZone: 'UTC',
        isActive: true,
        createdAt: new Date(),
        updatedAt: new Date(),
      });
      wh = await whColl.findOne({ _id: ins.insertedId });
    }
    const whId = String(wh?._id);

    const ownColl = await collections.stockOwners(org.orgDatabaseUri.trim());
    let ownerA = await ownColl.findOne({ code: 'CLIENT-A' });
    let ownerB = await ownColl.findOne({ code: 'CLIENT-B' });
    if (!ownerA) {
      const a = await ownColl.insertOne({
        code: 'CLIENT-A',
        name: 'Client A',
        isActive: true,
        createdAt: new Date(),
        updatedAt: new Date(),
      });
      ownerA = await ownColl.findOne({ _id: a.insertedId });
    }
    if (!ownerB) {
      const b = await ownColl.insertOne({
        code: 'CLIENT-B',
        name: 'Client B',
        isActive: true,
        createdAt: new Date(),
        updatedAt: new Date(),
      });
      ownerB = await ownColl.findOne({ _id: b.insertedId });
    }
    const ownerAId = String(ownerA?._id);
    const ownerBId = String(ownerB?._id);

    const locColl = await collections.locations(org.orgDatabaseUri.trim());
    let recvLoc = await locColl.findOne({ path: 'RECV-ACCEPT', warehouseId: whId });
    let storeLoc = await locColl.findOne({ path: 'A-ACCEPT-01', warehouseId: whId });
    if (!recvLoc) {
      const r = await locColl.insertOne({
        warehouseId: whId,
        path: 'RECV-ACCEPT',
        type: 'receiving',
        isActive: true,
        isPickable: false,
      });
      recvLoc = await locColl.findOne({ _id: r.insertedId });
    }
    if (!storeLoc) {
      const s = await locColl.insertOne({
        warehouseId: whId,
        path: 'A-ACCEPT-01',
        type: 'storage',
        isActive: true,
        isPickable: true,
      });
      storeLoc = await locColl.findOne({ _id: s.insertedId });
    }
    const recvLocId = String(recvLoc?._id);
    const storeLocId = String(storeLoc?._id);

    const skuColl = await collections.skus(org.orgDatabaseUri.trim());
    const skuExists = await skuColl.findOne({ skuId: FILTER_SKU });
    if (!skuExists) {
      await skuColl.insertOne({
        skuId: FILTER_SKU,
        displayName: 'Filter',
        primaryUom: 'EA',
        status: 'active',
        createdAt: new Date(),
        updatedAt: new Date(),
      });
    }

    const opAEmail = 'acceptance-operator-a@staging.local';
    const opBEmail = 'acceptance-operator-b@staging.local';

    const ensureAppUser = async (email: string, name: string) => {
      const existing = await tenantMongo.findOrgAppUserByEmail(tenantUri, email);
      if (existing) return;
      await tenantMongo.insertOrgAppUser(tenantUri, {
        name,
        email,
        emailNormalized: email.toLowerCase(),
        role: 'Employee',
        partsAllTemplates: false,
        allowedTemplateIds: [],
        temporaryPasswordHash: '',
        invitedAtUtc: new Date(),
      });
    };
    await ensureAppUser(opAEmail, 'Acceptance Operator A');
    await ensureAppUser(opBEmail, 'Acceptance Operator B');

    await grants.upsert(adminCtx, {
      email: opAEmail,
      warehouseIds: [whId],
      ownerIds: [ownerAId],
      permissions: { receiving: true, qc: true, putaway: true, supervise: false },
    });
    await grants.upsert(adminCtx, {
      email: opBEmail,
      warehouseIds: [whId],
      ownerIds: [ownerBId],
      permissions: { receiving: true, qc: true, putaway: true, supervise: false },
    });

    const ctxA = await buildCtxForEmail(guard, {
      user: { email: opAEmail, orgCode, userId: 'acceptance-a' },
    });

    const expected = await receiving.createExpectedReceipt(ctxA, {
      ownerId: ownerAId,
      warehouseId: whId,
      externalPoId: 'PO-ACCEPT-100-FILTER',
    });
    await receiving.addExpectedLines(ctxA, expected.expectedReceiptId, {
      lines: [{ skuId: FILTER_SKU, qtyExpected: 100, primaryUom: 'EA' }],
    });
    steps.push({ step: 'expected_100', pass: true, detail: expected.expectedReceiptId });

    const first = await receiving.confirmGoodsReceipt(ctxA, {
      idempotencyKey: IDEM_KEY,
      ownerId: ownerAId,
      warehouseId: whId,
      receivingLocationId: recvLocId,
      expectedReceiptId: expected.expectedReceiptId,
      lines: [{ skuId: FILTER_SKU, qtyReceived: 60 }],
    });
    steps.push({ step: 'receipt_60', pass: first.duplicate === false, detail: first.goodsReceiptId });

    const dup = await receiving.confirmGoodsReceipt(ctxA, {
      idempotencyKey: IDEM_KEY,
      ownerId: ownerAId,
      warehouseId: whId,
      receivingLocationId: recvLocId,
      expectedReceiptId: expected.expectedReceiptId,
      lines: [{ skuId: FILTER_SKU, qtyReceived: 60 }],
    });
    steps.push({
      step: 'idempotency_duplicate',
      pass: dup.duplicate === true,
      detail: `duplicate=${dup.duplicate}`,
    });

    const lines = await receiving.listReceiptLines(ctxA, first.goodsReceiptId);
    const lineId = String((lines.items[0] as { receiptLineId: string }).receiptLineId);
    await receiving.applyQc(ctxA, lineId, {
      status: 'partial',
      qtyAccepted: 55,
      qtyRejected: 5,
      note: 'Acceptance QC',
    });
    steps.push({ step: 'qc_partial_55_5', pass: true, detail: lineId });

    const putaways = await receiving.listPutaway(ctxA, whId);
    const taskId = String((putaways.items[0] as { putawayTaskId: string }).putawayTaskId);
    await receiving.completePutaway(ctxA, taskId, {
      idempotencyKey: 'acceptance-putaway-55',
      toLocationId: storeLocId,
    });
    steps.push({ step: 'putaway_55', pass: true, detail: taskId });

    const balColl = await collections.stockBalances(org.orgDatabaseUri.trim());
    const recvBal = await balColl.findOne({ skuKey: FILTER_SKU, locationId: recvLocId, ownerId: ownerAId });
    const storeBal = await balColl.findOne({ skuKey: FILTER_SKU, locationId: storeLocId, ownerId: ownerAId });
    const onHandRecv = Number(recvBal?.onHand ?? 0);
    const blockedRecv = Number(recvBal?.blockedQty ?? 0);
    const onHandStore = Number(storeBal?.onHand ?? 0);
    const totalOnHand = onHandRecv + onHandStore;
    const reconOk =
      totalOnHand === 60 && blockedRecv === 5 && onHandStore === 55;
    steps.push({
      step: 'reconciliation',
      pass: reconOk,
      detail: `recvOH=${onHandRecv} blocked=${blockedRecv} storeOH=${onHandStore}`,
    });

    const expectedDoc = await (
      await collections.expectedReceipts(org.orgDatabaseUri.trim())
    ).findOne({ expectedReceiptId: expected.expectedReceiptId });
    const row = (expectedDoc?.lines as Array<Record<string, unknown>>)?.[0];
    const remainOk =
      Number(row?.qtyReceivedTotal) === 60 && String(expectedDoc?.status) === 'partially_received';
    steps.push({
      step: 'expected_remain_40',
      pass: remainOk,
      detail: `status=${expectedDoc?.status} received=${row?.qtyReceivedTotal}`,
    });

    const ctxB = await buildCtxForEmail(guard, {
      user: { email: opBEmail, orgCode, userId: 'acceptance-b' },
    });
    let crossOwner403 = false;
    try {
      await receiving.createExpectedReceipt(ctxB, {
        ownerId: ownerAId,
        warehouseId: whId,
      });
    } catch (e) {
      crossOwner403 = e instanceof ForbiddenException;
    }
    steps.push({
      step: 'cross_owner_403',
      pass: crossOwner403,
      detail: crossOwner403 ? 'Forbidden as expected' : 'Did not reject',
    });
  } finally {
    await app.close();
  }

  const report = {
    executedAt: new Date().toISOString(),
    orgCode: process.env.STAGING_ORG_CODE,
    preflight: preflightDoc
      ? {
          orgCode: preflightDoc.orgCode,
          tenantDatabase: preflightDoc.tenantDatabase,
          replicaSetName: preflightDoc.replicaSetName,
          supported: preflightDoc.supported,
          commitProbe: preflightDoc.commitProbe,
          rollbackProbe: preflightDoc.rollbackProbe,
          deploymentTier: preflightDoc.deploymentTier,
          stagingStockPosting: preflightDoc.stagingStockPosting,
          liveStockPosting: preflightDoc.liveStockPosting,
          postingGateOpen: preflightDoc.postingGateOpen,
        }
      : null,
    steps,
    allPass: steps.every((s) => s.pass),
    winUiManual: 'NOT_EXECUTED',
    livePosting: false,
  };

  const outPath =
    process.env.STAGING_ACCEPTANCE_REPORT_JSON ??
    resolve(__dirname, '../../docs/wms-phase03/03A-2-staging-acceptance-evidence.json');
  writeFileSync(outPath, JSON.stringify(report, null, 2), 'utf8');
  console.log(JSON.stringify(report, null, 2));
  console.log(`\nWrote ${outPath}`);

  process.exit(report.allPass ? 0 : 1);
}

run().catch((err) => {
  console.error(err);
  process.exit(1);
});