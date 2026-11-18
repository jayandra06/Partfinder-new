# Phase 02B preparation — final readiness report

**Date:** 2026-11-17  
**Phase:** 02B preparation (design only) — **no** live stock posting  
**Prior gate:** 02A.1 security hardening provisionally accepted

---

## 1. Proposed canonical SKU design

- **Registry:** `wms_skus` with immutable **`skuId`** (ULID), used as `skuKey` on all balances and ledger lines.
- **Cross-refs:** `wms_sku_identifiers` with namespace-scoped uniqueness (master data row, parts row, MPN+manufacturer, supplier PN, ERP material, barcode).
- **Master data / parts remain** system of record for attributes; WMS does not duplicate full product rows.

**Detail:** `02B-sku-key-mapping.md`, `02B-database-proposal.md` §1.

---

## 2. Justification from source code

| Observation | Source |
|-------------|--------|
| Import **always** allocates new `ObjectId` | `MongoMasterDataRecordsService.AppendRowsAsync` → `ObjectId.GenerateNewId()` per row |
| Rows scoped by `templateId` | `MasterDataRowDoc.TemplateId` |
| IDs stable only when upsert keeps same `rowId` | `SaveRowsAsync` / `UpsertRowAsync` |
| Rows deletable / template clear | `DeleteRowAsync`, `ClearTemplateAsync` |
| Legacy inventory weak link | `InventoryDoc.PartId` string, separate from master ObjectId |
| Parts API identity | `parts` schema: `orgId`, `templateId`, `rowId` |

**Rejected:** Using master data `_id` alone as permanent WMS stock key.

---

## 3. Mapping and migration rules

| Rule | Behaviour |
|------|-----------|
| New SKU | Admin registers `skuId`, then attaches identifiers |
| Master data link | `masterDataRow` namespace with `templateId` in metadata |
| Legacy inventory dry-run | Match via identifiers + heuristics; unmapped → report |
| Re-import | New row IDs → **suspected duplicate** workflow, not auto relink |
| Merge | Explicit `merged` + `mergedIntoSkuId`; txn-backed in implementation phase |

---

## 4. Duplicate and ambiguous SKU handling

- **No** merge on MPN/barcode match alone.
- MPN unique per **manufacturer name** (or canonical mfr id).
- Ambiguity report in dry-run and inbound ERP validation (future).
- PO resolves collisions before balances go live.

---

## 5. Stock ledger architecture

Quantity states, txn types, invariants (pick/pack do not reduce physical total; dispatch does): **`02B-technical-specification.md`** §2–4.

Collections: `wms_stock_balances`, `wms_stock_transactions`, `wms_idempotency_keys`.

Posting: Mongo transactions only; idempotency; optimistic concurrency; immutable ledger + controlled reversals.

---

## 6. ERP integration architecture

Canonical connector + mapping + inbound idempotency + transactional outbox.  
Supports SAP, Oracle, Dynamics, NetSuite, Odoo, custom — **schema and interfaces only**.

ERP never holds parallel authoritative physical stock — WMS ledger + events.

**Detail:** `02B-technical-specification.md` §6.

---

## 7. MongoDB staging transaction evidence

| Check | Result (this task) |
|-------|---------------------|
| API probe procedure documented | **Yes** — `mongodb-transaction-preflight.md` |
| Probe executed on named staging tenant in CI | **No** — requires tenant URI in ops environment |
| Prior 02A manual probe (team report) | **Recorded** in `02A-gate-signoff.md` — re-confirm after entitlement change |

**Action before live 02B post:** Attach probe JSON + optional `mongosh` output per org in change ticket (template in preflight doc).

---

## 8. Security test results

**Executed 2026-11-17:**

```bash
cd PartFinder-Backend
npm test -- --testPathPatterns=wms|wms-entitlement
```

**Result:** **PASS** — 4 suites, **18** tests (entitlement, org resolution, session guard, scope util).

| Area | Covered by automation |
|------|------------------------|
| `wmsEnabled === true` only | `wms-entitlement.util.spec.ts` |
| JWT org required; cross-tenant header | `wms-org-resolution.util.spec.ts`, `wms-session.guard.spec.ts` |
| Employee warehouse/owner scope | `wms-scope.util.spec.ts` |
| Empty operator grants | `wms-session.guard.spec.ts` |

**Not executed (reported separately):**

- `npm run test:e2e`
- Full backend `npm test` (all suites)
- Live pen-test / AT-16 full portal scenarios
- Manual re-smoke after SKU doc change (recommended for pilot org)

---

## 9. Remaining product-owner decisions

| # | Decision |
|---|----------|
| 1 | Approve ULID `wms_skus` registry (vs alternatives) |
| 2 | Approve namespace uniqueness rules (MPN+mfr, etc.) |
| 3 | Name master data column for `primaryUom` |
| 4 | Approve merge/retire SKU workflow |
| 5 | Approve opening-balance dry-run policy before any live post |
| 6 | Approve ERP outbox event catalog (minimal set for 02B) |

**Sign:** `02B-sku-approval-summary.md` checklist.

---

## 10. Phase 02B implementation boundaries

### May start after PO + B1 evidence

- Implement `wms_skus`, `wms_sku_identifiers` APIs + indexes.
- Implement ledger **schema** + posting service **disabled by default** or `dryRunOnly` flag.
- Opening balance **dry-run** endpoint (no commit).
- ERP collection schemas + stub inbound validator.
- Automated tests from `02B-acceptance-test-plan.md` (T-SKU-*, T-LDG-*).

### Must not ship until explicit approval

- Writing production `wms_stock_balances` from opening balance or receive.
- Reservation, pick, pack, dispatch mutations.
- Live ERP connectors or bidirectional sync.

---

## Request for approval

Please approve:

1. **Canonical SKU strategy** (`02B-sku-approval-summary.md`), and  
2. **Inventory posting implementation** per `02B-technical-specification.md` and `02B-database-proposal.md`.

**Until signed:** Phase 02B remains **design-only**; no live inventory posting in repository deployments.

---

## Deliverables index

| Document |
|----------|
| `02B-sku-key-mapping.md` (revised) |
| `02B-sku-approval-summary.md` (revised) |
| `02B-technical-specification.md` (revised) |
| `02B-database-proposal.md` (new) |
| `02B-acceptance-test-plan.md` (new) |
| `02B-final-readiness-report.md` (this file) |
| `02A-1-security-readiness-report.md` |
| `mongodb-transaction-preflight.md` |