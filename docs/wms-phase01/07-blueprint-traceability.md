# 07 — Functional Blueprint Traceability (revised)

**Sources:** `PartFinder_WMS_Cursor_Functional_Blueprint.md` v1.0, `PartFinder_WMS_Phase01_Architecture_Decisions.md`  
**Cross-ref:** `03-wms-architecture.md`, `04-wms-database-proposal.md`, `05-wms-development-roadmap.md`, `06-wms-risks-and-questions.md`, `08-architecture-decisions.md`

**Legend:** E = exists/reuse, M = modify, N = new

---

## Approved architecture overrides (traceability)

| Requirement | Was (Phase 01 draft) | **Approved** |
|-------------|----------------------|--------------|
| WMS API | Hybrid; optional for devices | **Mutations** mandatory NestJS (D01); **reads** scoped via API (D01b) |
| Desktop Mongo | Read cache possible | Cache **invalidatable**; never stock truth |
| Legacy inventory | Global freeze | **Per-warehouse cutover** (D17) |
| Mongo txn fallback | Compensating updates | **Block posting** if unsupported (D08) |
| 3PL owners | Phase 02 foundation | **Phase 02A** (D02) |
| Print | Phase 06 | **Phase 04** with pack (D21, D26); ZPL = P01 |
| Entitlement | — | **D25** on all WMS routes (02A design) |
| Carton seal vs order | — | Independent seal (D27); reconcile before close |
| Lot/serial | Pack lines only | **Balances, movements, reservations, contents** (D22) |

---

## Requirement traceability (selected rows)

| Blueprint requirement | Existing code | Change | Phase | Tests |
|----------------------|---------------|--------|-------|-------|
| Mandatory WMS API | `MongoInventoryService` direct writes (legacy only) | N: `WmsApiClient`; mutations + scoped reads | 02A+ | AT-1, AT-16 |
| WMS licence / limits | `verifyLicense` (`maxUsers`, `maxParts`) | M: `wmsEnabled`, warehouse/owner limits (D25) | 02A | AT-16 |
| Quantity / reconciliation | — | N: server ATP + order reconcile (D27) | 03–04 | AT-6, AT-11 |
| Stock owners (3PL) | — | N: `wms_stock_owners` + API scope | **02A** | AT-2, AT-16 |
| Warehouse/location setup | Single `location` string on `InventoryDoc` | N: `wms_warehouses`, `wms_locations` | 02A | — |
| Ledger + immutability | — | N: ledger service | **02B** | AT-13, AT-17 |
| Reserve / ATP model | — | N: reservations | 03 | AT-4, AT-13 |
| Pick staging, on-hand invariant | — | N: pick confirm API | 03 | AT-5, AT-13 |
| Task claim concurrent | — | N: `wms_pick_tasks` atomic claim | 03 | AT-3, AT-19 |
| Pack verify mandatory | — | N: package contents API | 04 | AT-6, AT-8 |
| Customer PN pouch label | Master data columns only | N: order line field + PDF | 04 | AT-7 |
| Carton/pallet hierarchy | — | N: `wms_packages` | 04 | AT-9 |
| PDF print + preview/reprint | `QrCodeManager` (parts only) | N: `wms/print` PDF | **04** | AT-8, AT-10, AT-18 |
| Dispatch deduct actual only | `InventoryDoc.quantity` upsert | N: `DISPATCH_POSTED` txn | 04 | AT-11, AT-12, AT-13 |
| Partial dispatch exception | — | N: approval workflow | 04 | AT-6, AT-11 |
| Procurement alerts | `MongoAlertsService`, `minStock` | N: post-dispatch, deduped | 04 | AT-14, AT-15 |
| Lot/serial traceability | — | N: optional fields on balance/txn/content | 02B–04 | AT-2 ext. |
| Order import idempotency | `import/` CSV | N: external order keys | 03 | AT-20 |
| Legacy inventory coexistence | `InventoryPage` | M: cutover flag per warehouse | 07 / cutover | AT-1 |
| Excel worksheets | `MasterDataPage` patterns | E: WMS setup sheets | 02A | AT-1 |
| Auth JWT | `SetupApiClient`, invite-login | E + M: WMS guards | 02A | AT-16 |

---

## Contradictions — resolution (updated)

| # | Issue | Resolution (approved) |
|---|--------|------------------------|
| C1 | Grid/cell vs DB truth | All WMS via API; worksheets are views |
| C2 | Multi-device pick | Nest task APIs Phase 03 |
| C3 | 3PL owners | Owners Phase **02A**; scope on server |
| C4 | Immutable ledger | 02B append-only |
| C5 | Customer PN | Order line Phase 04 |
| C6 | Retail | Separate workbook |
| C7 | Order import | Phase 03 idempotent API |
| C8 | Legacy inventory | **D17** cutover — not global freeze |
| C9 | Optional WMS API (old diagram) | **Removed** — mandatory API |

---

## Acceptance tests → phase (authoritative mapping)

See `05-wms-development-roadmap.md` § Acceptance test mapping. Summary:

| Phase | Tests exercised when feature ships |
|-------|-----------------------------------|
| **02A** | AT-1 (structure), AT-16 (auth reject), regression |
| **02B** | AT-2, AT-13 (pre-dispatch math), AT-17 |
| **03** | AT-3, AT-4, AT-5, AT-19, AT-20 |
| **04** | AT-6–AT-12, AT-14, AT-15, AT-18; AT-13 completion |
| **05+** | Inbound/returns; AT-17 ongoing |
| **Cutover / 07** | AT-1 legacy coexistence |

---

## Module → phase (19 modules)

| Module # | Name | First substantial phase |
|----------|------|-------------------------|
| 01 | Company & Warehouse Setup | 02A |
| 02 | Users & Permissions | 02A (design); enforce 02A–03 |
| 03 | Master Data | E — link `skuKey` 02B |
| 04 | Excel Data Management | E — order import 03 |
| 05–08 | Inbound / QC / Put-away | 05 |
| 09 | Inventory Management | 02B ledger views |
| 10–11 | Movements / Counts | 05 |
| 12 | Outbound orders | 03 |
| 13 | Picking | 03 |
| 14 | Packing + **print** | **04** |
| 15 | Dispatch | 04 |
| 16 | Returns | 05 |
| 17 | Warehouse admin | 02A+ |
| 18 | Reports / procurement | 04–05 |
| 19 | 3PL / labels | 02A owners; 04 labels; 06 commercial |

---

## Status

- Phase 01 audit + **architecture corrections:** complete.  
- Phase 02A implementation: **awaiting explicit PO authorization** (`06-wms-risks-and-questions.md`).  
- Phase 02B: **blocked** on Mongo transaction verification (B1) unless PO waives.