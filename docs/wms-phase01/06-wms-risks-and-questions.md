# 06 — WMS Risks, Blockers, and Decision Log

**Last updated:** 2026-11-16 (final consistency & safety review)  
**Authoritative decision register:** **This file (D01–D25, P01–P04)** is the **only** in-repo decision register.  
`08-architecture-decisions.md` is a navigation index to this log and the other Phase 01 docs — it must not contradict D/P IDs here.  
External PO attachment `PartFinder_WMS_Phase01_Architecture_Decisions.md` is superseded where IDs differ.

---

## Technical risks (updated)

| Risk | Severity | Mitigation / status |
|------|----------|---------------------|
| Desktop direct Mongo for WMS | **High** | **Approved:** all **WMS inventory mutations** via NestJS (D01); operational **reads** via API with tenant/warehouse/owner enforcement (D01b); WinUI may cache read-only snapshots |
| Legacy `partfinder_inventory` dual truth | **High** | Per-warehouse cutover (D17); not global freeze |
| Mongo multi-doc transactions unavailable | **Critical** | **Block** ledger posting; remediate deployment — **no** compensating stock saga |
| NestJS → tenant `orgDatabaseUri` not proven in hosting | **Critical** | Phase 02A task B; mark blocker until documented |
| Casbin sync without route enforcement | **High** | Enforce on every `wms` route in 02A+ |
| Casbin + owner scope incomplete | **Medium** | Design owner matrix in 02A |
| PDF/print dependency approval | **Medium** | Phase 04; D14 before npm add |
| Staging tenant missing | **High** | D15 required for integration acceptance |
| Master data stable SKU identity | **Medium** | **Block 02B** until B6 passes (D19) |
| WMS not on subscription | **High** | No `wms` feature flag today — implement D25 in 02A+ (B7) |

**Removed:** “fallback to compensating transactions for inventory posting” — **not permitted** per approved architecture.

---

## Technical blockers before Phase 02B (stock posting)

| ID | Blocker | Verification action | Owner |
|----|---------|---------------------|-------|
| B1 | MongoDB supports **multi-document transactions** on WMS staging tenant | `mongosh` `hello` / txn test script on staging replica set | Eng + ops |
| B2 | Backend can **connect to tenant Mongo** securely using stored URI | Integration test in 02A; secrets not in client | Eng |
| B3 | **JWT → tenant** mapping implemented and tested | 02A auth tests | Eng |
| B4 | **Staging tenant** provisioned (D15) | Ops / PO | |
| B5 | Casbin or equivalent **route guards** on WMS (not sync-only) | 02A deliverable | Eng |
| B6 | **Stable SKU identity** review (master data row ids / mapping) | Document + PO sign-off before `wms_stock_balances` | Eng + PO |
| B7 | **WMS subscription entitlement** check on every WMS route | Extend platform org/plan or feature flags; enforce limits | Eng + PO |

**Stock-changing code:** Do **not** merge ledger, reservation, dispatch, or balance mutation paths until **B1** (transactions) and **B6** (SKU) are satisfied. **02A** setup CRUD may proceed without B1/B6.

Phase 02A may proceed **without** B1 for setup-only CRUD if no stock mutations (approved). Phase 02B **cannot** start until **B1** and **B6** are **passed** (B7 required before any WMS route is exposed outside internal dev).

---

## Data protection

| Rule | Action |
|------|--------|
| No production data deletion | Additive `wms_*` collections only |
| Legacy inventory | Untouched until cutover procedure |
| Cutover | Dry-run, discrepancy report, backup, explicit approval |
| Audit | Server-side append-only for WMS (D18) |

---

## Open questions (remaining)

### Business

| # | Question | Status |
|---|----------|--------|
| 1 | Multiple 3PL clients per org from day one? | **Resolved:** Yes — Phase 02A (D02) |
| 2 | Retail SKU sharing? | **Resolved:** Separate workbooks (D03) |
| 3 | Carrier standards v1? | **Resolved (PDF):** internal PDF docs (D04); carrier formats **proposed** (P01) |
| 4 | Partial shipments? | **Proposed default (P03):** block full order complete unless reconciled; manager exception — PO may override |
| 5 | Procurement notifications? | **Resolved:** In-app + email, deduped (D06) |
| 6 | Second-person pack check mandatory? | Configurable per owner (blueprint); default TBD in 04 |
| 7 | 3PL client self-service portal v1? | **Proposed default (P04):** reports/email first; portal Phase 06+ |

### Technical

| # | Question | Status |
|---|----------|--------|
| 6 | Handheld platform? | **Approved:** WinUI + API-ready (D07); **scanner hardware models (P02)** proposed default |
| 7 | Mongo replica set for all tenants? | **Open** — verify B1 per deployment |
| 8 | Integration API keys? | **Resolved:** Not in v1 (D09) |
| 9 | Barcodes? | **Resolved:** Code 128 + optional QR (D10) |
| 10 | Elasticsearch for WMS? | **Resolved:** Mongo indexes first (D11) |
| 11 | Offline WMS? | **Resolved:** No offline mutation v1 (D12) |
| 12 | Time zones? | **Resolved:** UTC store, warehouse-local display (D13) |
| 13 | Dependency approval process? | **Resolved:** D14 |
| 14 | Staging tenant? | **Required** D15 |
| 15 | Nav: WMS workbook vs Inventory? | **Resolved:** Separate WMS workbook (D16) |

### Pending verification (not product decisions)

1. NestJS secure tenant DB connectivity under production hosting  
2. Transaction support **per** tenant Mongo deployment  
3. Stable master-data / parts identity (D19)  
4. Endpoint enforcement gap today  
5. Whether any CI hits production Mongo  
6. Printer hardware / label stock in warehouses  

---

## Decision log (approved — supersedes earlier draft entries)

| ID | Topic | Decision | Date |
|----|-------|----------|------|
| D01 | WMS mutations | **Mandatory** authenticated NestJS for **all new WMS inventory mutations** (ledger, reserve, pick, pack, dispatch, adjust) | 2026-11-16 |
| D01b | WMS reads | Operational inventory/order **reads** via NestJS with **tenant + warehouse + stock-owner** permission enforcement; WinUI may hold **read-only** cache, not stock truth | 2026-11-16 |
| D02 | 3PL owners | Multiple stock owners per org from **Phase 02A**; `ownerId` on all stock entities | 2026-11-16 |
| D03 | Retail | Separate workbook; no Retail in WMS programme | 2026-11-16 |
| D04 | Labels v1 (PDF) | **PDF** pouch/carton/pallet/list/delivery note before **live dispatch** (D21, D26); Code 128 on package IDs (D10) | 2026-11-16 |
| D05 | Partial dispatch policy | See **P03** (proposed default until PO reconfirms) | 2026-11-16 |
| D06 | Procurement alerts | In-app + email; post-commit; deduplicated | 2026-11-16 |
| D07 | Handheld | WinUI + barcode wedge first | 2026-11-16 |
| D08 | Mongo transactions | Multi-doc **required** for ledger posts; **block** if unsupported; **no** compensating fallback | 2026-11-16 |
| D09 | Integration partners | First-party signed-in clients only v1 | 2026-11-16 |
| D10 | Barcodes | Code 128 internal; optional QR; GS1 when validated | 2026-11-16 |
| D11 | Search | Mongo indexes + paginated API | 2026-11-16 |
| D12 | Offline | No offline WMS mutation v1 | 2026-11-16 |
| D13 | Time zones | UTC storage; warehouse-local display | 2026-11-16 |
| D14 | New packages | PO review before print/queue libs | 2026-11-16 |
| D15 | Staging | Required for E2E mutation tests | 2026-11-16 |
| D16 | Navigation | Separate WMS Workbook; legacy Inventory remains | 2026-11-16 |
| D17 | Legacy cutover | Per-warehouse migration; no global disable; no dual-write after cutover | 2026-11-16 |
| D18 | Audit | Server-side append-only; reversals controlled | 2026-11-16 |
| D19 | SKU canon | Master data `skuKey`; customer PN on **order line** | 2026-11-16 |
| D20 | Packing verify | Mandatory line-level verify before **package** seal; **carton may seal** when its contents verified even if order incomplete (D27) | 2026-11-16 |
| D25 | WMS entitlement | Every WMS API checks org **subscription + WMS feature** and applicable limits (warehouses, owners, users) | 2026-11-16 |
| D26 | Dispatch gate | No **live** dispatch until pack verify + **basic** PDF print path (pouch/carton/packing list) exists | 2026-11-16 |
| D27 | Order vs package seal | Independent carton/pallet seal; **block order `closed` / full dispatch** until `qtyOrdered` reconciles with verified packed/dispatched (or approved partial) | 2026-11-16 |
| D28 | Retail / legacy | **Retail Management** separate workbook (D03); **existing PartFinder Inventory** unchanged globally (D16, D17) | 2026-11-16 |
| D21 | Print timing | PDF print suite in **Phase 04** with packing/dispatch | 2026-11-16 |
| D22 | Lot / serial | Optional on balances, movements, reservations, package contents | 2026-11-16 |
| D23 | Dispatch deduct | Only verified shipped qty; pick/pack do not reduce total on-hand | 2026-11-16 |
| D24 | Auth | Tenant from **verified JWT**; not client-only `orgCode` header | 2026-11-16 |

### Proposed defaults (not fully PO-approved — implement unless contradicted)

| ID | Topic | Proposed default | Phase |
|----|-------|------------------|-------|
| P01 | PDF vs ZPL | **PDF first** for all v1 documents; ZPL only after hardware/template validation | 04+ |
| P02 | Scanner hardware | USB keyboard-wedge scanners; no camera scanning v1 | 03–04 |
| P03 | Partial shipment | Block marking order **fully** complete/dispatched if verified qty &lt; ordered; manager exception with reason code | 04 |
| P04 | 3PL client portal | Email/report exports before self-service web portal | 06 |

### Superseded (do not use)

| Old | Was | Now |
|-----|-----|-----|
| D1 (2026-11-16 early) | “Mutations via API; desktop read/cache” | **D01** + **D01b** — mutations API; reads scoped (cache optional) |
| D3 early | “Freeze partfinder_inventory” | **D17** — per-warehouse cutover |
| D9 early | “Lot on pack lines only” | **D22** — lot/serial on stock + movements + pack |
| Risk row | Compensating txn fallback | **Removed** — use D08 |

---

## Phase 02A authorization readiness

| Criterion | Ready? | Notes |
|-----------|--------|-------|
| Phase 01 docs reconciled (single register) | **Yes** | This file + `03`–`05`, `07` |
| Phase 02A scope (no stock mutations) | **Yes** | Setup CRUD, auth, entitlement design |
| **Authorization to start Phase 02A code** | **Conditional yes** | Explicit PO go-ahead only |

**Not ready for Phase 02B / any stock-changing work** until **B1**, **B6**, **B7** satisfied.

---

## Final readiness checklist (requirement × status)

| # | Requirement | Approved (D/P) | Technically verified | Open |
|---|-------------|----------------|----------------------|------|
| 1 | Preserve legacy Inventory; per-warehouse cutover only | **D17, D28** | Partial (code unchanged today) | Cutover tooling later |
| 2 | WMS mutations via Nest; reads scoped | **D01, D01b, D24** | **No** — no `wms` module | B2, B3, B5, B7 in 02A |
| 3 | No compensating multi-record inventory posts | **D08** | **No** | **B1** on staging |
| 4 | Quantity model without double-count | **D23** + `03` §3, `04` §0 | Design only | Implement in 02B |
| 5 | Seal cartons independently; block full order until reconcile | **D20, D27** | Design only | Phase 04 |
| 6 | Pack verify + basic PDF before live dispatch | **D26, D21, D04** | **No** | Phase 04; **D14** for npm |
| 7 | Lot/serial through receive→dispatch | **D22** | Design only | 02B–04 |
| 8 | WMS subscription entitlement + limits | **D25** | **No** — org has `maxUsers`/`maxParts` only | **B7** schema + guard |
| 9 | Stable SKU before owner balances | **D19** | **No** | **B6** review |
| 10 | PDF/ZPL, scanner, partial, portal defaults | **P01–P04** | N/A (defaults) | PO may override |
| 11 | Retail separate; all current features | **D03, D28** | Yes (no Retail in repo) | — |
| 12 | One authoritative decision register | **This doc** | Yes | Remove any stray duplicates |

**Stop rule:** No stock-changing functionality (balances, ledger posts, reservations, dispatch deduct) until rows **3, 8, 9** move from Open to verified.

---

## Compliance / audit

- WMS stock events: **server authoritative** (D18).  
- GDPR: owner-scoped data; cross-owner queries forbidden (AT-16).  
- Legacy desktop audit logs remain non-authoritative for WMS.