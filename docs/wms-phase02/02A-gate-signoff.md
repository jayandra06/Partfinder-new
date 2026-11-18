# Phase 02A — Gate sign-off

**Gate:** Approve start of **Phase 02B** (ledger design only in repo until this gate is accepted).  
**Scope closed:** WMS setup masters + auth + WinUI **WMS Setup** — **no stock mutations**.  
**References:** `02A-implementation-report.md`, `docs/wms-phase01/05-wms-development-roadmap.md`, `06-wms-risks-and-questions.md`.

---

## Sign-off summary

| Field | Value |
|-------|--------|
| **Gate decision** | **02A accepted** — proceed to **02B planning / implementation** per roadmap |
| **Sign-off date** | 2026-11-17 |
| **Tested by** | Engineering (manual acceptance — all §6 steps completed) |
| **Builds** | WinUI `x64`; NestJS `npm run build` |
| **API base used in test** | Environment under team control (local `start:dev` and/or deployed licence API) |
| **Test organisation** | Six-digit org used in test (e.g. `884470` — replace if different in your run) |
| **Tenant Mongo** | Organisation `orgDatabaseUri` from platform setup |

---

## Manual acceptance (02A §6)

All steps **passed** in the manual run reported on **2026-11-17**.

| # | Step | Result |
|---|------|--------|
| 1 | Backend `npm run start:dev` with valid `.env` | **PASS** |
| 2 | Platform org has `orgDatabaseUri`; licence JWT works | **PASS** |
| 3 | WinUI login; `LicenseApi:BaseUrl` reaches backend | **PASS** |
| 4 | Admin nav **WMS Setup** opens workbook | **PASS** |
| 5 | Create warehouse, stock owner, zone, location (e.g. WH01 / CLIENT1) | **PASS** |
| 6 | Legacy **Warehouse Management** still writes `partfinder_inventory` | **PASS** |
| 7 | WMS API without token → **401**; bad `X-Org-Id` vs JWT → **403** | **PASS** |

**Regression noted:** Login, templates, master data, legacy inventory — no WMS-related regressions observed in this cycle.

---

## Readiness checklist (B1–B7)

Per `06-wms-risks-and-questions.md`. **02B stock-changing code** requires **B1**, **B6**, and **B7** before merge to shared environments.

| ID | Blocker | Status | Evidence / action |
|----|---------|--------|-------------------|
| **B1** | Multi-doc Mongo transactions on **tenant** DB | **PASS (test env)** | WMS Setup transaction probe and/or `GET /api/wms/v1/diagnostics/transactions` succeeded for test tenant. **Ops:** retain `hello` / replica-set proof for each production tenant before ledger go-live. |
| **B2** | Backend connects to tenant Mongo using stored URI | **PASS** | Setup CRUD persisted in `wms_*` collections on tenant DB. |
| **B3** | JWT → tenant mapping | **PASS** | Authenticated WMS calls with `Authorization` + `X-Org-Id`. |
| **B4** | Staging tenant (D15) | **PASS (test org)** | Manual tests used non-production org; confirm dedicated staging org for automated E2E later. |
| **B5** | Route guards on WMS (not sync-only) | **PASS** | `WmsSessionGuard` on `/api/wms/v1/*`; 401/403 verified. |
| **B6** | Stable SKU / `skuKey` sign-off | **PENDING PO** | Revised registry design in **`02B-sku-key-mapping.md`** — PO signs **`02B-sku-approval-summary.md`** (not master data `_id` as key). |
| **B7** | `wmsEnabled` + licence on org | **02A.1 hardened** | **Opt-in only** (`wmsEnabled === true`). Pilot orgs must be set explicitly; new orgs default **false**. See `02A-1-security-readiness-report.md`. |

---

## Deliverables verified (02A)

| Deliverable | Status |
|-------------|--------|
| `/api/wms/v1` health + setup CRUD | Shipped |
| Tenant `wms_warehouses`, `wms_stock_owners`, `wms_zones`, `wms_locations` | Shipped |
| WinUI `WmsApiClient` + **WMS Setup** page | Shipped |
| Legacy `partfinder_inventory` path unchanged | Confirmed |
| Ledger, orders, pick, pack, dispatch | **Not in scope** (02B+) |

---

## Phase 02A.1 — Security & readiness (2026-11-17)

| Evidence | Location |
|----------|----------|
| Hardening report | `02A-1-security-readiness-report.md` |
| JWT org binding + entitlement tests | `PartFinder-Backend` `src/wms/guards/*.spec.ts`, `wms-entitlement.util.spec.ts` |
| Mongo txn preflight | `mongodb-transaction-preflight.md` |
| ERP foundation (design) | `02B-technical-specification.md` |
| SKU PO summary | `02B-sku-approval-summary.md` |

**Remaining approvals:** PO on SKU summary; ops B1 evidence per tenant; set `wmsEnabled: true` only on licensed WMS orgs.

---

## Programme position

| Measure | Estimate |
|---------|----------|
| Roadmap implementation phases **02A–07** | **1 / 6** complete (~17%) |
| Full WMS functional blueprint (end-to-end) | Foundation only (~10–15%) |

---

## Approved next work (Phase 02B)

Start only after **B6 PO sign-off** on `02B-sku-key-mapping.md` (or superseding doc).

1. Add `wms_stock_balances`, `wms_stock_transactions` (additive collections).
2. Posting service — **Mongo multi-doc transactions only** (no compensating saga).
3. Read APIs for balances + ledger lines; WinUI reads via API (no desktop Mongo stock truth).
4. Opening balance **dry-run** from `partfinder_inventory` — no per-warehouse cutover yet.

**Stop rule (unchanged):** No order UI, reservation production paths, or dispatch until later phases.

---

## Approvals

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Engineering lead | | 2026-11-17 | |
| Product owner | | | *Required for B6 before 02B balances* |
| Ops (Mongo / deploy) | | | *Required for production `backend.partfinder.store`* |

---

*This file is the formal 02A gate record. Update the table above if your test org or API host differ from the values noted.*