# Phase 03A.2 — Staging acceptance runbook

Use this guide to move **`03A-2-staging-acceptance-report.md`** from **NOT ACCEPTED** to **ACCEPTED**.  
Disposable integration tests in CI are **not** a substitute for steps on a **named staging tenant**.

---

## 1. Provision staging stack

| Component | Requirement |
|-----------|-------------|
| API host | Separate from production; `WMS_DEPLOYMENT_TIER=staging`, `WMS_STAGING_STOCK_POSTING=true`, `WMS_LIVE_STOCK_POSTING=false` |
| Platform Mongo | Organization with `wmsEnabled: true`, dedicated `tenantUri` to isolated tenant DB |
| Tenant Mongo | Replica set (Atlas M10+ or self-hosted RS) — transactions required |
| WinUI clients | API base URL → staging host; operators sign in with staging org code |

### Local staging-lab Mongo (optional, developers)

```powershell
cd PartFinder-Backend
docker compose -f docker/wms-staging-mongo-compose.yml up -d
# Tenant URI example (platform org record): mongodb://127.0.0.1:27018/your_tenant_db?replicaSet=wmsStagingRs
```

Point **one pilot org** at this URI only for lab work — not production data.

---

## 2. Seed master data (staging org)

1. Sign in as org admin → **Warehouse Workbook**.
2. Create warehouse `WH-DEMO-01`, receiving location `RECV-01`, storage `A-01-01`.
3. Create stock owners **Client A**, **Client B**.
4. Register SKU (display name **Filter**), UOM **EA**.
5. **Administration → Record preflight** (or CLI below).
6. **Operator grants:**
   - Operator A: warehouses scoped to demo WH, owners **Client A** only; receiving + QC + put-away.
   - Operator B: owners **Client B** only (negative test).

---

## 3. Record transaction preflight evidence

### Option A — WinUI (preferred for acceptance)

1. WMS admin → Workbook → **Administration**.
2. Optional note → **Record preflight**.
3. Confirm **Receiving gate summary** shows `supported=true`, `commit=true`, `rollback=true`, `stagingPost=true`.

### Option B — CLI (paste JSON into report §2)

```powershell
cd PartFinder-Backend
$env:WMS_API_BASE = "https://<staging-api-host>"
$env:WMS_JWT = "<admin bearer token>"
$env:WMS_PREFLIGHT_NOTE = "03A.2 acceptance"
npm run wms:staging-preflight
```

Redact any hostnames if attaching to git; store secrets only in tickets.

---

## 4. Optional: API acceptance runner (same logic as WinUI POSTs)

On a machine with platform Mongo + tenant access (isolated staging org):

```powershell
cd PartFinder-Backend
$env:STAGING_ORG_CODE = "900xxx"   # dedicated staging org — not production customer org
$env:WMS_DEPLOYMENT_TIER = "staging"
$env:WMS_STAGING_STOCK_POSTING = "true"
npm run wms:staging-acceptance
```

Writes `docs/wms-phase03/03A-2-staging-acceptance-evidence.json` (gitignored). Paste redacted `preflight` + failed steps into the acceptance report. **Does not replace** WinUI manual sign-off.

List candidate orgs: `node scripts/list-wms-staging-orgs.mjs` (requires DNS fix — run `npm run wms:mongodb-dns-preflight` first on Windows if SRV fails).

Named tenant harness:

**Recommended (CMD or PowerShell):**

```bat
cd PartFinder-Backend
npm run wms:mongodb-dns-preflight
npm run wms:03a2-staging-acceptance
```

If you set the flag manually:

| Shell | Command |
|-------|---------|
| **PowerShell** | `$env:WMS_RUN_STAGING_ACCEPTANCE = "true"` then `npm test -- --testPathPatterns=wms-03a2-staging-acceptance --runInBand` |
| **CMD** | `set WMS_RUN_STAGING_ACCEPTANCE=true` then `npm test -- --testPathPatterns=wms-03a2-staging-acceptance --runInBand` |

Do **not** paste `$env:...` into **CMD** — that causes “filename syntax is incorrect” and the suite will be **skipped**.

---

## 5. Execute WinUI checklist (100 filters)

Follow **`03A-2-staging-acceptance-report.md` §3** as Operator A. Capture:

- Status messages after each POST
- Balance worksheet totals (60 on-hand, 5 blocked at RECV, 55 in storage)
- Export XLSX for expected / receipts / lines
- Screenshot of **Staging gate** line after preflight

Repeat confirm with **same idempotency key** (step 9).  
Sign in as Operator B and attempt owner A receipt (step 10) — expect **403**.

---

## 6. Update acceptance report

1. Paste sanitized preflight JSON into §2.
2. Mark §3 table **Pass** / **Fail** per step.
3. Fill §4 physical reconciliation from API or balance UI.
4. Engineering lead sign-off on §4 + §7 test table.
5. Change verdict at top to **ACCEPTED** or **ACCEPTED WITH CAVEATS**.

---

## 7. What this workspace cannot automate

- Issuing staging JWTs or platform `tenantUri` values
- Interactive WinUI manual runs inside Cursor
- Proof of production isolation on your real hosts (ops attestation)

---

*Related:* `mongodb-transaction-preflight.md`, `03A-2-staging-acceptance-report.md`