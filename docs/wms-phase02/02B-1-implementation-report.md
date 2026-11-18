# Phase 02B.1 — Implementation Report

**Status:** Controlled dev complete (ledger, balances, ERP outbox foundation, WinUI read worksheets).  
**Production posting:** OFF (`WMS_LIVE_STOCK_POSTING` unset / not `true`).  
**Staging posting:** OFF unless `WMS_STAGING_STOCK_POSTING=true` (plus MongoDB transactions on tenant).

---

## Source files

### Backend (added)

| File | Role |
|------|------|
| `wms-staging-stock.util.ts` | Staging vs production posting gates |
| `wms-balance-key.util.ts` | Balance key + ATP compute |
| `wms-stock-transaction.types.ts` | Txn types and command shape |
| `wms-inventory-plan.util.ts` | Move conservation helpers |
| `dto/wms-inventory.dto.ts` | Posting + ERP mapping DTOs |
| `services/wms-transaction-guard.service.ts` | Fail-closed txn probe (cached) |
| `services/wms-inventory-posting.service.ts` | Central `post()` with Mongo session txn |
| `services/wms-inventory-read.service.ts` | Balances, ledger, ATP reads |
| `*.spec.ts` | Balance, staging gate, merge guards, move plan, namespaces |

### Backend (modified)

| File | Change |
|------|--------|
| `wms-tenant-collections.service.ts` | Balances, txns, idempotency, ERP mappings, outbox + `ensurePhase02B1Indexes()` |
| `wms-ledger-dry-run.service.ts` | Extended opening-balance report |
| `wms-erp-foundation.service.ts` | SKU/location mappings, outbox list, connector errors, inbound `externalDocumentId` |
| `wms-sku.service.ts` | Merge/retire stock guards; sanitized identifier conflicts |
| `wms-phase02.controller.ts` | Inventory + ERP mapping/outbox routes |
| `wms-setup.controller.ts` | Health `02B.1`, staging/live flags |
| `wms.module.ts` | New providers |

### WinUI (modified)

| File | Change |
|------|--------|
| `WmsApiClient.cs` | SKUs, balances, transactions, opening dry-run DTOs |
| `WmsWorkbookViewModel.cs` | Search, pagination, worksheet loaders |
| `WmsWorkbookPage.xaml` | Phase 02B.1 tabs (SKU, balances, ledger, dry-run, errors) |

Phase 02A setup tabs unchanged.

---

## MongoDB collections & indexes

| Collection | Purpose |
|------------|---------|
| `wms_stock_balances` | Materialized on-hand (`onHand`, `reserved`, `blockedQty`, `version`) |
| `wms_stock_transactions` | Append-only ledger |
| `wms_idempotency_keys` | Post dedup (`key` unique, TTL on `expiresAt`) |
| `wms_erp_sku_mappings` | External material → `skuId` |
| `wms_erp_location_mappings` | External loc → WMS warehouse/location |
| `wms_outbox_events` | Durable outbound events (same txn when `emitErpEvent`) |

**Balance unique key:** `balanceKey` = owner + warehouse + location + skuId + lot + serial.

**Ledger:** No updates/deletes; reversals append with `reversalOf`.

**Legacy:** `partfinder_inventory` is read-only for dry-run; never written by WMS posting.

---

## API endpoints (JWT + `WmsSessionGuard`)

| Method | Path | Notes |
|--------|------|-------|
| POST | `/api/wms/v1/inventory/post` | Staging/live gated; txn required |
| GET | `/api/wms/v1/inventory/balances` | Paginated; owner/warehouse scope |
| GET | `/api/wms/v1/inventory/transactions` | Read-only ledger |
| GET | `/api/wms/v1/inventory/atp` | Computed ATP only |
| GET | `/api/wms/v1/ledger/opening-balance-dry-run` | Expanded report |
| GET/POST | `/api/wms/v1/erp/sku-mappings` | |
| GET/POST | `/api/wms/v1/erp/location-mappings` | |
| GET | `/api/wms/v1/erp/outbox` | Pending events (no publisher) |

Existing 02B SKU/UOM/ERP connector routes preserved.

---

## Stock transaction behavior

| Type | Admin | Behavior |
|------|-------|----------|
| `openingBalance` | Yes | +qty at `toLocationId` |
| `receive` | Operator | +qty at `toLocationId` |
| `move` | Operator | −from, +to; warehouse total unchanged |
| `adjust` | Yes | Signed delta; `approvalNote` required |
| `writeOff` | Yes | −qty; `approvalNote` required |
| `reversal` | Yes | Inverts prior txn by `reversalOf` |
| `ownerTransfer` | Yes | −source owner, +target owner (same wh/loc) |

- Negative on-hand rejected; on-hand cannot drop below `reserved`.
- Optimistic `version` on balances with bounded retries.
- Idempotency: duplicate key + same hash → replay; different hash → `409`.
- Optional `emitErpEvent` writes `wms_outbox_events` in the **same** Mongo transaction.
- ERP inbound stub **does not** post stock.

---

## WinUI worksheets

| Worksheet | Implementation |
|-----------|----------------|
| SKU Registry | API list, searchable |
| SKU Identifier Mapping | Read-only guidance (API for mutations) |
| Inventory Stock Balances | Paginated read-only grid |
| Warehouse Location Stock | Location column from balances |
| Stock Movement Ledger | Paginated read-only |
| Opening Balance Import Preview | Dry-run summary text |
| SKU Mapping Errors | Derived from dry-run counts |

Setup tabs (warehouses, owners, zones) unchanged.

---

## Transaction preflight evidence

| Check | How |
|-------|-----|
| API | `GET /api/wms/v1/diagnostics/transactions` (per authenticated tenant) |
| WinUI | WMS Workbook shows probe summary on load |
| Server | `WmsTransactionGuardService` blocks `inventory/post` if probe fails |

**Not attached in this workspace:** live output for a named staging tenant (requires deployed backend + pilot org). Run manually before first staging post and attach JSON to the ticket per `mongodb-transaction-preflight.md`.

---

## Automated tests

```text
npm run build
# exit 0

npm test -- --testPathPatterns=wms
# Test Suites: 9 passed, 9 total
# Tests: 29 passed, 29 total
```

| ID (acceptance plan) | Coverage |
|----------------------|----------|
| T-SKU-* | namespaces + merge guards |
| T-ATP-01 | `computeAtp` unit test |
| T-SEC-01 | existing 02A.1 guard tests |
| T-LDG-01..06 | **Partial** — posting logic not run against real Mongo in CI |
| T-ERP-01 | inbound idempotency stub (manual/API) |

### Failed / not executed (needs staging Mongo + `WMS_STAGING_STOCK_POSTING=true`)

- Concurrent post / rollback / double-post integration
- Full owner-isolation balance API e2e
- Opening dry-run on production-like dataset (manual)
- PartFinder WinUI / Retail regression suite (not run in this task)
- `npm run test:e2e` (unchanged)

---

## Opening balance dry-run

Enhanced report fields: `totalSourceRows`, mapped/unmapped, `invalidQuantities`, `invalidUom`, `duplicateCandidates`, missing default owner/warehouse, `proposedOpeningByWarehouseOwner`. **No automatic post**; legacy inventory untouched.

**Workspace execution:** No tenant DB connected here — counts are **not** populated in this report.

---

## Security findings

- Identifier conflicts: non-admin responses omit foreign `existingSkuId`.
- Merge/retire blocked when `onHand > 0`; merge with ledger history requires `reason`.
- Cross-owner paths require `ownerTransfer` + grants (existing 02A.1).
- Production posting remains double-gated (`WMS_LIVE_STOCK_POSTING` not enabled).

---

## Remaining blockers (Phase 03 readiness)

| Blocker | Owner |
|---------|--------|
| PO sign-off on live/staging stock activation | Product |
| B1 txn evidence on target tenant(s) | Ops |
| Pilot `wmsEnabled` orgs + staging env flag | Ops |
| Integration tests against Mongo replica set | Engineering |
| WinUI identifier CRUD worksheet | Optional UX |
| ERP publisher worker (outbox drain) | Phase 05+ |

---

## Readiness for Phase 03

**Data model:** `reserved` on balances reserved for reservations; ATP computed only.  
**Ledger:** Immutable with reversal path.  
**ERP:** Outbox + mappings ready; no live connectors.

**Do not start:** pick/pack/dispatch, procurement alerts, live ERP, or production cutover without further authorization.