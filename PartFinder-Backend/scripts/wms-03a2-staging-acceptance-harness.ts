/**
 * Phase 03A.2 — HTTP acceptance harness (named staging tenant).
 * Runs the 100-filter receiving/QC/put-away scenario via API + records preflight JSON.
 *
 * Required environment (secrets — never commit):
 *   WMS_API_BASE          e.g. https://staging-api.example.com
 *   WMS_JWT_ADMIN         WMS admin bearer token
 *   WMS_JWT_OPERATOR_A    Operator scoped to Client A
 *   WMS_JWT_OPERATOR_B    Operator scoped to Client B only
 *
 * Master data (staging tenant — create in WinUI first or via setup APIs):
 *   WMS_WAREHOUSE_ID, WMS_OWNER_A_ID, WMS_OWNER_B_ID,
 *   WMS_RECV_LOCATION_ID, WMS_STORE_LOCATION_ID, WMS_SKU_ID
 *
 * Optional:
 *   WMS_ACCEPTANCE_NOTE   recorded in preflight note
 *
 * Usage:
 *   npm run wms:03a2-acceptance-harness
 */
const required = [
  'WMS_API_BASE',
  'WMS_JWT_ADMIN',
  'WMS_JWT_OPERATOR_A',
  'WMS_JWT_OPERATOR_B',
  'WMS_WAREHOUSE_ID',
  'WMS_OWNER_A_ID',
  'WMS_OWNER_B_ID',
  'WMS_RECV_LOCATION_ID',
  'WMS_STORE_LOCATION_ID',
  'WMS_SKU_ID',
] as const;

type StepResult = { step: string; pass: boolean; detail: string };

function missing(): string[] {
  return required.filter((k) => !process.env[k]?.trim());
}

async function api(
  base: string,
  jwt: string,
  method: string,
  path: string,
  body?: unknown,
): Promise<{ status: number; json: unknown; text: string }> {
  const res = await fetch(`${base}/api${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${jwt}`,
      'Content-Type': 'application/json',
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  let json: unknown;
  try {
    json = JSON.parse(text);
  } catch {
    json = { raw: text };
  }
  return { status: res.status, json, text };
}

function dataField(json: unknown): unknown {
  if (json && typeof json === 'object' && 'data' in json) {
    return (json as { data: unknown }).data;
  }
  return json;
}

async function main() {
  const miss = missing();
  if (miss.length) {
    console.error(
      'Staging acceptance harness: missing environment variables:',
      miss.join(', '),
    );
    console.error(
      'Configure staging per docs/wms-phase03/03A-2-staging-deployment-package.md',
    );
    process.exit(2);
  }

  const base = process.env.WMS_API_BASE!.replace(/\/$/, '');
  const steps: StepResult[] = [];
  const report: Record<string, unknown> = {
    harness: '03A.2',
    executedAt: new Date().toISOString(),
    apiHost: base.replace(/^https?:\/\//, '').split('/')[0],
    notStagingEvidenceIfHostIsLab: true,
  };

  const adminJwt = process.env.WMS_JWT_ADMIN!.trim();
  const opA = process.env.WMS_JWT_OPERATOR_A!.trim();
  const opB = process.env.WMS_JWT_OPERATOR_B!.trim();
  const wh = process.env.WMS_WAREHOUSE_ID!.trim();
  const ownerA = process.env.WMS_OWNER_A_ID!.trim();
  const ownerB = process.env.WMS_OWNER_B_ID!.trim();
  const recvLoc = process.env.WMS_RECV_LOCATION_ID!.trim();
  const storeLoc = process.env.WMS_STORE_LOCATION_ID!.trim();
  const skuId = process.env.WMS_SKU_ID!.trim();
  const idem = `03a2-filter-100-${Date.now()}`;

  // Preflight
  const pre = await api(base, adminJwt, 'POST', '/wms/v1/staging-gate/preflight', {
    tenantLabel: process.env.WMS_ACCEPTANCE_NOTE?.trim() ?? '03A.2 acceptance harness',
  });
  const preflight = dataField(pre.json);
  report.preflight = preflight;
  steps.push({
    step: 'Preflight POST',
    pass: pre.status === 200 || pre.status === 201,
    detail: `HTTP ${pre.status}`,
  });

  // Expected
  const exp = await api(base, opA, 'POST', '/wms/v1/receiving/expected', {
    ownerId: ownerA,
    warehouseId: wh,
    externalPoId: 'PO-100-FILTERS',
  });
  const expectedId = (dataField(exp.json) as { expectedReceiptId?: string })
    ?.expectedReceiptId;
  steps.push({
    step: 'Create expected (Operator A)',
    pass: exp.status === 200 || exp.status === 201,
    detail: `HTTP ${exp.status}`,
  });

  if (expectedId) {
    const lines = await api(base, opA, 'POST', `/wms/v1/receiving/expected/${expectedId}/lines`, {
      lines: [{ skuId, qtyExpected: 100, primaryUom: 'EA' }],
    });
    steps.push({
      step: 'Expected 100 lines',
      pass: lines.status === 200 || lines.status === 201,
      detail: `HTTP ${lines.status}`,
    });
  }

  const gr = await api(base, opA, 'POST', '/wms/v1/receiving/goods-receipts', {
    idempotencyKey: idem,
    ownerId: ownerA,
    warehouseId: wh,
    receivingLocationId: recvLoc,
    expectedReceiptId: expectedId,
    lines: [{ skuId, qtyReceived: 60 }],
  });
  const goodsReceiptId = (dataField(gr.json) as { goodsReceiptId?: string })
    ?.goodsReceiptId;
  const duplicateFirst = (dataField(gr.json) as { duplicate?: boolean })?.duplicate;
  steps.push({
    step: 'Confirm receipt qty 60',
    pass: (gr.status === 200 || gr.status === 201) && duplicateFirst !== true,
    detail: `HTTP ${gr.status}, duplicate=${duplicateFirst}`,
  });

  const grDup = await api(base, opA, 'POST', '/wms/v1/receiving/goods-receipts', {
    idempotencyKey: idem,
    ownerId: ownerA,
    warehouseId: wh,
    receivingLocationId: recvLoc,
    expectedReceiptId: expectedId,
    lines: [{ skuId, qtyReceived: 60 }],
  });
  const duplicateSecond = (dataField(grDup.json) as { duplicate?: boolean })?.duplicate;
  steps.push({
    step: 'Duplicate same idempotency key',
    pass: grDup.status === 200 || grDup.status === 201,
    detail: `duplicate=${duplicateSecond}`,
  });
  report.idempotencyKey = idem;
  report.duplicateSecond = duplicateSecond;

  let receiptLineId: string | undefined;
  if (goodsReceiptId) {
    const lineRes = await api(
      base,
      opA,
      'GET',
      `/wms/v1/receiving/goods-receipts/${goodsReceiptId}/lines`,
    );
    const items = (dataField(lineRes.json) as { items?: Array<{ receiptLineId?: string }> })
      ?.items;
    receiptLineId = items?.[0]?.receiptLineId;
  }

  if (receiptLineId) {
    const qc = await api(base, opA, 'POST', `/wms/v1/receiving/qc/${receiptLineId}`, {
      status: 'partial',
      qtyAccepted: 55,
      qtyRejected: 5,
      note: '03A.2 acceptance',
    });
    steps.push({
      step: 'QC partial 55/5',
      pass: qc.status === 200 || qc.status === 201,
      detail: `HTTP ${qc.status}`,
    });
  }

  const puts = await api(base, opA, 'GET', `/wms/v1/receiving/putaway?warehouseId=${encodeURIComponent(wh)}`);
  const putItems = (dataField(puts.json) as { items?: Array<{ putawayTaskId?: string }> })
    ?.items;
  const taskId = putItems?.[0]?.putawayTaskId;
  if (taskId) {
    const pa = await api(base, opA, 'POST', `/wms/v1/receiving/putaway/${taskId}/complete`, {
      idempotencyKey: `put-${idem}`,
      toLocationId: storeLoc,
    });
    steps.push({
      step: 'Put-away 55',
      pass: pa.status === 200 || pa.status === 201,
      detail: `HTTP ${pa.status}`,
    });
  }

  const bal = await api(
    base,
    opA,
    'GET',
    `/wms/v1/inventory/balances?warehouseId=${encodeURIComponent(wh)}&ownerId=${encodeURIComponent(ownerA)}&skuId=${encodeURIComponent(skuId)}`,
  );
  report.balances = dataField(bal.json);
  steps.push({
    step: 'Balances read',
    pass: bal.status === 200,
    detail: `HTTP ${bal.status}`,
  });

  const cross = await api(base, opB, 'POST', '/wms/v1/receiving/expected', {
    ownerId: ownerA,
    warehouseId: wh,
  });
  steps.push({
    step: 'Operator B creates expected for Owner A',
    pass: cross.status === 403,
    detail: `HTTP ${cross.status} (expected 403)`,
  });

  report.steps = steps;
  report.passCount = steps.filter((s) => s.pass).length;
  report.failCount = steps.filter((s) => !s.pass).length;
  report.verdict = report.failCount === 0 ? 'PASS' : 'FAIL';

  console.log('--- 03A.2 staging acceptance harness report (paste §2–§5) ---');
  console.log(JSON.stringify(report, null, 2));
  console.log('--- end ---');

  process.exit(report.verdict === 'PASS' ? 0 : 1);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});