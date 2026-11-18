# Phase 03A.2 — Staging Acceptance Report

**Verdict:** **NOT ACCEPTED** (full gate) — **Named-tenant API acceptance: PASS** (org **900152**, 2026-11-18); **WinUI manual + hosted staging API: pending**.  
**Production stock posting:** **OFF** — `liveStockPosting: false` in acceptance evidence; production tier ignores staging flag.  
**Phase 03B:** **Do not start** until WinUI checklist complete and §11 sign-off (or explicit PO waiver).

---

## Database connectivity fix — Tasks 1–4 (summary)

| Deliverable | Finding |
|-------------|---------|
| **1. Backend hosting** | **Frontend:** Vercel (`Partfinder-Portfolio`) proxies `/api/*` to production API only. **Production NestJS:** `https://backend.partfinder.store` (WinUI default, portfolio rewrites). **Staging NestJS:** **Not defined in repo**; Dokploy application search (`partfinder`) returned **0** apps — ops must confirm where the API actually runs (likely non-Vercel VM/container; **not** documented here). **Failed `querySrv` test environment:** **Local Node/Jest** on engineer workstation using platform `MONGODB_URI` from `PartFinder-Backend/.env` (gitignored), **not** Vercel serverless and **not** a named staging API host. |
| **2. Root cause (`querySrv ECONNREFUSED`)** | Node `dns.getServers()` was **`127.0.0.1` only**; SRV lookup to localhost DNS was **refused**. Windows `nslookup` via LAN resolver **`192.168.1.1`** succeeded for the same Atlas SRV name. Not an Atlas credential issue; not fixed by `0.0.0.0/0` allow-list. Detail: [`03A-2-database-connectivity.md`](./03A-2-database-connectivity.md). |
| **3. Fix applied** | `mongodb-atlas-dns` (merge `192.168.1.1`, `1.1.1.1`, `8.8.8.8`; `ipv4first`) in `scripts/mongodb-atlas-dns.mjs` and `src/test-utils/mongodb-atlas-dns.ts`; wired into acceptance scripts/specs. Verify: `npm run wms:mongodb-dns-preflight` → **PASS** (re-checked 2026-11-18). |
| **4. Staging isolation** | Org **900152** (`WMS Staging Acceptance`, `wmsEnabled: true`) → tenant DB **`wms_staging_acceptance_900152`**. Production org **111111** → tenant DB **`111111`**; `wmsEnabled: false`. Same Atlas cluster host class; **logical** isolation via `orgDatabaseUri` / DB name (no cross-route without platform doc change). Acceptance Jest sets `WMS_DEPLOYMENT_TIER=staging`, `WMS_STAGING_STOCK_POSTING=true`, `WMS_LIVE_STOCK_POSTING=false`. Production tier **ignores** staging posting flag in code. |
| **5. Acceptance re-run (safe)** | After DNS fix: **`wms-03a2-staging-acceptance` PASS** (~94s) — preflight `supported`/commit/rollback **true**, 100-filter ledger scenario **60/55/5/40**, idempotency + cross-owner **403**. Evidence: [`evidence/03a2-staging-evidence.json`](./evidence/03a2-staging-evidence.json). **Does not** substitute WinUI sign-off. |
| **6. Remaining blockers** | (1) **No staging API hostname** in repo — HTTP preflight on deployed host not done. (2) **WinUI checklist** all pending — [`03A-2-winui-acceptance-checklist.md`](./03A-2-winui-acceptance-checklist.md). (3) Production server env (`WMS_*` on `backend.partfinder.store`) unverified from repo. (4) §11 sign-off pending. **Ops/DBA if needed:** provision staging NestJS + confirm Atlas **Network Access** for that host egress IP (not opened to internet in this task). |

**Redacted org lookup (2026-11-18, `staging-org-lookup.mjs`):**

| orgCode | name | wmsEnabled | tenantDatabaseName |
|---------|------|------------|-------------------|
| 900152 | WMS Staging Acceptance | true | `wms_staging_acceptance_900152` |
| 111111 | Euroasiann Group | false | `111111` |

### Latest execution (2026-11-18)

| Activity | Result |
|----------|--------|
| DNS root cause | **Identified** — Node used `127.0.0.1` only; see [`03A-2-database-connectivity.md`](./03A-2-database-connectivity.md) |
| DNS fix | **Applied** — `mongodb-atlas-dns` + `npm run wms:mongodb-dns-preflight` **PASS** |
| `wms-03a2-staging-acceptance` (org **900152**, Atlas tenant DB) | **PASS** — evidence in [`evidence/03a2-staging-evidence.json`](./evidence/03a2-staging-evidence.json) |
| WinUI 100-filter checklist | **NOT EXECUTED** — checklist **prepared** for operator QA |
| Hosted staging NestJS | **NOT IN REPO** — test ran **local Jest** with staging posting flags |
| Production `WMS_LIVE_STOCK_POSTING` | **Not enabled** |

**Next:** WinUI against an API with `WMS_DEPLOYMENT_TIER=staging` and `WMS_STAGING_STOCK_POSTING=true`, org **900152** — [`03A-2-winui-acceptance-checklist.md`](./03A-2-winui-acceptance-checklist.md). Prefer **`npm run wms:03a2-staging-acceptance`** to re-run API harness (sets env + DNS).

| Package | Link |
|---------|------|
| Deployment (env, org, WinUI URL) | [`03A-2-staging-deployment-package.md`](./03A-2-staging-deployment-package.md) |
| Runbook (preflight + seed) | [`03A-2-staging-runbook.md`](./03A-2-staging-runbook.md) |
| WinUI checklist (100 filters) | [`03A-2-winui-acceptance-checklist.md`](./03A-2-winui-acceptance-checklist.md) |
| Automated gate script | `PartFinder-Backend/scripts/run-03a2-automated-gate.ps1` |
| Atlas DNS preflight | `npm run wms:mongodb-dns-preflight` |
| Named-tenant acceptance (API) | `npm run wms:03a2-staging-acceptance` (or PowerShell: `$env:WMS_RUN_STAGING_ACCEPTANCE='true'; npm test -- --testPathPatterns=wms-03a2-staging-acceptance --runInBand`) |
| Staging org lookup (redacted) | `node scripts/staging-org-lookup.mjs 900152` |
| Connectivity notes | [`03A-2-database-connectivity.md`](./03A-2-database-connectivity.md) |

---

## 1. Backend hosting and staging status (Task 1)

| Layer | Location | Evidence |
|-------|----------|----------|
| Marketing / portfolio site | **Vercel** (`Partfinder-Portfolio`) | `vercel.json` rewrites `/api/*` → `https://backend.partfinder.store/api/*` (frontend is **not** the API) |
| Production NestJS API | **`https://backend.partfinder.store`** | WinUI `LicenseApiClient` default; portfolio dev proxy targets same host |
| Staging NestJS API | **Not defined in repository** | No staging hostname in tree; Dokploy catalog (connected MCP) returned **no** PartFinder apps |
| Where acceptance ran | **Developer machine** — Jest `wms-03a2-staging-acceptance.spec.ts` | Connects to **Atlas** using platform `MONGODB_URI` from local `.env` (secrets not in git) |
| Production API posting flags | **Unknown from repo** — ops must run `config:diagnostics` **on the server** | Do not assume staging posting on `backend.partfinder.store` without server env proof |

**Implication:** Org **900152** acceptance proves **ledger + preflight on isolated tenant DB**; it does **not** prove the **deployed** production API has staging posting enabled (desired: production tier + no staging flag on that host).

### Staging environment (no secrets)

| Component | Finding |
|-----------|---------|
| Test org **900152** | **Provisioned** — `wmsEnabled: true`, tenant DB `wms_staging_acceptance_900152` |
| Production org **111111** | Tenant DB name **`111111`** — **different database** on same Atlas cluster (logical isolation) |
| WinUI API target | Still **`backend.partfinder.store`** unless QA overrides `appsettings.json` |
| Production isolation | **Enforced in code** — `WMS_DEPLOYMENT_TIER=production` ignores `WMS_STAGING_STOCK_POSTING=true` |

### Infrastructure / access required (ops)

1. **Staging NestJS** host (separate from production) with:
   - `WMS_DEPLOYMENT_TIER=staging`
   - `WMS_STAGING_STOCK_POSTING=true`
   - `WMS_LIVE_STOCK_POSTING` unset or `false`
2. **Platform organization** (e.g. dedicated 6-digit org code) with `wmsEnabled` and entitlement limits.
3. **Tenant MongoDB** on replica set / Atlas tier supporting transactions (isolated DB name; **no customer data**).
4. **JWT users:**
   - WMS admin (preflight + grants + setup).
   - Operator A scoped to stock owner **Client A** + test warehouse.
   - Operator B scoped to **Client B** only (negative test).
5. **WinUI** pointed at staging API URL; sign-in with staging org.
6. Seed: warehouse, receiving + storage locations, SKU “Filter”, stock owners A/B, operator grants.

See also: `docs/wms-phase02/mongodb-transaction-preflight.md`.

---

## 2. Transaction preflight evidence (Task 2–4)

| Item | Status |
|------|--------|
| `POST /api/wms/v1/staging-gate/preflight` via HTTP on hosted API | **NOT EXECUTED** — no staging API host |
| Preflight via **service** on tenant **900152** (acceptance spec) | **PASS** — recorded in tenant `wms_staging_gate_evidence` |
| MongoDB multi-document transactions | **PASS** — `supported: true`, `commitProbe: true`, `rollbackProbe: true`, `replicaSetName: atlas-52zzyo-shard-0` |

### Recorded staging evidence (redacted — 2026-11-18)

```json
{
  "orgCode": "900152",
  "tenantDatabase": "wms_staging_acceptance_900152",
  "replicaSetName": "atlas-52zzyo-shard-0",
  "supported": true,
  "commitProbe": true,
  "rollbackProbe": true,
  "deploymentTier": "staging",
  "stagingStockPosting": true,
  "liveStockPosting": false,
  "postingGateOpen": true
}
```

Full scenario: [`evidence/03a2-staging-evidence.json`](./evidence/03a2-staging-evidence.json).

### Code delivered in 03A.2

Preflight now records **session-derived identity**, not a standalone label:

- `orgCode`, `organizationName` (from JWT session)
- `tenantDatabase` (connection DB name only)
- `replicaSetName` (from `hello`)
- `commitProbe`, `rollbackProbe` (insert + deliberate rollback)
- `deploymentTier`, `stagingStockPosting`, `liveStockPosting`, `postingGateOpen`, `wmsEnabled`
- Optional operator `note` — supplementary only

### Staging isolation evidence (org 900152)

| Check | Result |
|-------|--------|
| Org code | **900152** — name `WMS Staging Acceptance` (test org) |
| Tenant database name | **`wms_staging_acceptance_900152`** ≠ production **`111111`** |
| Cross-routing to prod tenant | **Not possible** without platform `orgDatabaseUri` change (manual ops action) |
| Stock posts during test | **Staging tier only** in test process env |
| Production customer orgs | **Not mutated** by acceptance spec (scoped to 900152 tenant) |

**After staging login (admin) — for hosted API proof:**

```http
POST /api/wms/v1/staging-gate/preflight
Authorization: Bearer <staging JWT>
Content-Type: application/json

{ "tenantLabel": "optional note only" }
```

```http
GET /api/wms/v1/staging-gate/latest
```

**CLI alternative (staging host):** `npm run wms:staging-preflight` with `WMS_API_BASE` + `WMS_JWT` — see runbook §3.

**WinUI alternative:** Workbook → Administration → **Record preflight** (WMS admin).

Paste **redacted** JSON fields into this report (no URIs, no secrets).

---

## 3. WinUI end-to-end test results

**Manual execution in Cursor:** **NOT PERFORMED** (no authorized staging API + interactive WinUI session in agent environment).  
**Executable checklist:** [`03A-2-winui-acceptance-checklist.md`](./03A-2-winui-acceptance-checklist.md).

### Operator dropdown / scope (engineering verification)

| Check | Method | Result |
|-------|--------|--------|
| API lists filter warehouses by grant | `wms-setup-operator-scope.spec.ts` | **Pass** |
| API lists filter owners by grant | Same | **Pass** |
| WinUI combos bind to filtered API lists | Code review (`ReloadMastersAsync` → `ListWarehousesAsync`) | **Pass** |
| Operator B cannot select Client A on staging UI | Manual step S4 / checklist step 10 | **Pending** |

### Operator checklist — 100 filters scenario

**WinUI:** All **Pending**.  
**API (Jest, org 900152):** Steps 2–7, 9–10 **Pass**; step 1 / 8 / 11 **Not validated in UI** (ledger types asserted in integration suites only).

| Step | Action | Expected | WinUI | API (900152) |
|------|--------|----------|-------|----------------|
| 1 | Sign in as Operator A | Workbook loads | **Pending** | N/A |
| 2 | Expected 100 × Filter | `open` | **Pending** | **Pass** |
| 3 | Receipt **60** + idempotency | `partially_received` | **Pending** | **Pass** |
| 4 | QC **55** / **5** | Blocked reject | **Pending** | **Pass** |
| 5 | Put-away **55** | Task complete | **Pending** | **Pass** |
| 6 | Balances | 60 / 55 / 5 | **Pending** | **Pass** |
| 7 | Expected remain | **40** | **Pending** | **Pass** |
| 8 | Movement ledger | txn types | **Pending** | **Not verified** (read API) |
| 9 | Duplicate idempotency | `duplicate: true` | **Pending** | **Pass** |
| 10 | Operator B → owner A | **403** | **Pending** | **Pass** |
| 11 | ERP outbox | Pending only | **Pending** | **Not verified** |

Attach screenshots or exported balance/ledger XLSX when complete.

---

## 4. Physical stock reconciliation

| Metric | Expected after scenario | Staging measured | Status |
|--------|-------------------------|------------------|--------|
| Physical on-hand (warehouse) | 60 | **60** | **Pass** (API) |
| Blocked (rejected at receiving) | 5 | **5** | **Pass** (API) |
| Available at storage | 55 | **55** (`onHandStorage`) | **Pass** (API) |
| Expected remaining | 40 | **40** | **Pass** (API) |

Source: `evidence/03a2-staging-evidence.json` → `scenario`.

---

## 5. Idempotency and authorization

| Test | Automated (replSet) | Staging UI |
|------|---------------------|------------|
| Duplicate goods receipt idempotency key | **Pass** (replSet) | **Pass** (900152 API) |
| Cross-owner create expected receipt | **Pass** (403) | **Pass** (900152 API) |

---

## 6. Frontend improvements (03A.2)

| Feature | Status |
|---------|--------|
| Client-side sort (receiving lists) | Added |
| Search filter on reload (workbook search box) | Added |
| Column width sliders (draft receipt grid) | Added |
| Clipboard paste (tab-separated receipt lines) | Added |
| Excel export (expected, goods receipts, receipt lines) | Added |
| Staging gate summary (from `GET staging-gate/latest`) | Added |
| **Record preflight** (`POST staging-gate/preflight`) in Administration | Added (03A.2 cont.) |
| Supervisor disposition UI (explicit write-off) | Added (QC worksheet) |
| Operator grants — permission checkboxes | Added |
| Operator grants — **warehouse / owner scope** checkboxes | Added (03A.2 cont.) |
| Virtualized grid / full Master Data parity | **Not implemented** (ListView-based) |
| Ledger/balances | **Read-only** (unchanged) |

---

## 7. Tests

| Suite | Result |
|-------|--------|
| `npm run build` | **Pass** (re-verified 03A.2 cont.) |
| `jest --testPathPatterns=wms` (no `--forceExit`) | **Pass** — 13 suites, 40 tests (tier gates + operator scope) |
| `jest --testPathPatterns=receiving-workflow.integration` | **Pass** (replSet; not staging evidence) |
| Integration teardown (`TenantMongoService.onModuleDestroy`) | **Fixed** |
| `dotnet build` PartFinder x64 | **Pass** (re-verified 03A.2 cont.) |
| `PartFinder.Tests` | **Pass** — 23/23 (re-verified 03A.2 cont.) |
| `run-03a2-automated-gate.ps1` | **Pass** (local, 2026-11-18) |
| `wms-03a2-staging-acceptance` (Atlas org 900152) | **Pass** (2026-11-18) |
| `npm run wms:mongodb-dns-preflight` | **Pass** |
| Staging WinUI manual | **Not executed** |
| Staging preflight on real tenant | **Not executed** |

---

## 8. Outstanding blockers

1. **No dedicated staging NestJS host** in repo — production WinUI still defaults to `backend.partfinder.store`.
2. **WinUI manual checklist** — all rows **Pending** (API ≠ WinUI sign-off).
3. **HTTP preflight** on deployed API not executed — only in-tenant evidence from Jest.
4. **Engineering / QA sign-off** (§11) — **Pending**.
5. ~~Atlas DNS `querySrv ECONNREFUSED`~~ — **resolved** (`mongodb-atlas-dns`).
6. ~~Org **900152** / isolated tenant~~ — **provisioned** and exercised.

---

## 9. Phase 03B readiness recommendation

| Criterion | Ready? |
|-----------|--------|
| Receiving/QC/put-away code path | **Yes** (integration-tested) |
| Staging gate + real tenant evidence | **No** |
| WinUI operator acceptance | **No** |
| Frontend Excel parity | **Partial** |

**Recommendation:** **Hold Phase 03B** until:

1. Staging environment per §1 is provisioned.
2. §2 preflight JSON attached (sanitized).
3. §3 checklist rows marked pass with evidence.
4. Engineering lead signs off §4 reconciliation.

**Explicitly not done:** production posting, live ERP, orders/picking/packing/dispatch.

---

## 10. Engineering artifacts (acceptance closure pack)

| Artifact | Path |
|----------|------|
| Staging deployment package | `docs/wms-phase03/03A-2-staging-deployment-package.md` |
| Staging runbook | `docs/wms-phase03/03A-2-staging-runbook.md` |
| WinUI acceptance checklist | `docs/wms-phase03/03A-2-winui-acceptance-checklist.md` |
| Automated gate (CI/local) | `PartFinder-Backend/scripts/run-03a2-automated-gate.ps1` |
| Preflight recorder CLI | `PartFinder-Backend/scripts/wms-record-staging-preflight.ts` |
| Env diagnostics (no secrets) | `npm run config:diagnostics` |
| Optional local Mongo RS | `PartFinder-Backend/docker/wms-staging-mongo-compose.yml` |
| Posting tier guard | `PartFinder-Backend/src/wms/wms-deployment-tier.util.ts` |

---

## 11. Engineering reviewer sign-off (mandatory for ACCEPTED)

| Role | Name | Date | Staging gate |
|------|------|------|--------------|
| Engineering lead | | | ☐ Pending |
| QA lead | | | ☐ Pending |

**Criteria to check:** §2 preflight JSON, §3 all Pass, §4 reconciliation, §5 staging UI rows, production host `npm run config:diagnostics` shows `wmsStagingPostingEffective: false`.

---

## 12. Screenshots / exports

| Asset | Status |
|-------|--------|
| Preflight / gate summary screenshot | **Not attached** |
| Balance reconciliation screenshot | **Not attached** |
| XLSX exports from receiving worksheets | **Not attached** |
| Operator B 403 screenshot | **Not attached** |

---

*Phase 03A.2 engineering closure pack delivered; **staging acceptance incomplete** until §2–§4 and §11 are completed on the named staging tenant.*