# 05 — WMS Development Roadmap (revised gates)

**Approved:** 2026-11-16 (reconciled with `06-wms-risks-and-questions.md`)  
**Retail Management:** separate workbook — **out of scope** (D03, D28). Legacy **Inventory** preserved until per-warehouse cutover (D17).  
**Rule:** Test acceptance criteria in the **same phase** that delivers the feature (see § Acceptance test mapping).  
**Stop rule:** No stock-changing code until **B1, B6, B7** satisfied (`06` § Final readiness checklist).

---

## Phase 02A — Secure workbook foundation (no stock mutations)

**Goal:** Navigable WMS workbook + server setup masters + auth proof.

| Work item | Deliverable |
|-----------|-------------|
| NestJS `wms` module skeleton | `/api/wms/v1/`, health, versioning |
| Tenant resolution | JWT → org → `orgDatabaseUri`; **no** stock APIs yet |
| Transaction topology check | Document staging Mongo replica-set / transaction support (**blocker if fail**) |
| CRUD APIs | Warehouses, zones, locations, **stock owners** (3PL clients) |
| Permissions | Server-enforced view/admin setup; owner/warehouse scope design |
| WMS entitlement | Design + implement guard: licence active + `wmsEnabled` + limits (D25, B7) |
| SKU identity review | Document master-data → `skuKey` mapping; **no balances** (B6) |
| WinUI WMS entry | `AppPage` + setup worksheets (Excel-style, API-backed) |
| Legacy | `InventoryPage` / `partfinder_inventory` **unchanged** |
| Collections | Additive setup collections only |

**Exit criteria / tests in this phase:**  
AT-1 (WMS isolation vs Retail — structure only), AT-16 (cross-owner API reject on setup if scoped), auth rejection tests, WinUI smoke CRUD, regression on login/templates/master-data/legacy inventory.  
**Zero** ledger posts, reservations, picks, packs, prints, dispatch, procurement messages.

**Deliverable doc:** `docs/wms-phase02/02A-implementation-report.md` (when implemented).

---

## Phase 02B — Ledger and balances (no dispatch UI)

**Goal:** Append-only ledger + balances with owner/location/lot dimensions; opening balances / controlled receipt test only.

| Work item | Deliverable |
|-----------|-------------|
| `wms_stock_balances`, `wms_stock_transactions` | Owner × warehouse × location × skuKey × optional lot/serial |
| Posting service | **Multi-doc transactions**; idempotency keys |
| Conservation invariants | Documented + integration tests (AT-13 partial) |
| Read APIs | Balances + ledger (mandatory API — WinUI read via Nest) |
| Opening balance | Controlled import/migration dry-run tool (**no** live cutover yet) |
| Procurement | Read-only projected view stub optional |

**Exit criteria / tests:** AT-2 (owner separation on balances), AT-13 (stock math scenarios without dispatch), AT-17 (immutability + reversal design test).  
**No** order UI, **no** dispatch deduction in production paths.

---

## Phase 03 — Outbound: orders, reserve, pick

**Goal:** Concurrent-safe reservation and picking into staging.

| Work item | Deliverable |
|-----------|-------------|
| Orders + lines | External idempotency (AT-20) |
| Reservations | Owner-aware; transactional cap (AT-4) |
| Pick tasks | Split lines, **atomic claim** (AT-3, AT-19) |
| Pick confirm | Staging transfer; on-hand total unchanged (AT-5) |
| WinUI | Order import queue, picking board |

**Exit criteria / tests:** AT-3, AT-4, AT-5, AT-19, AT-20.

---

## Phase 04 — Packing, printing, dispatch, procurement

**Goal:** Verified pack, **PDF print suite**, dispatch-only deduct, replenishment alerts. **No live dispatch** until pack verify + basic PDF (D26) complete in this phase.

| Work item | Deliverable |
|-----------|-------------|
| Package hierarchy | Pouch → carton → pallet (AT-9) |
| Pack verification | Mandatory before **package** seal; cartons may seal independently (D27, AT-6, AT-8) |
| Order reconciliation | Block `closed` until line qty reconcile or partial approved (D27, P03) |
| Customer PN on order line | Pouch PDF (AT-7) |
| **Print (PDF)** | Pouch, carton, pallet, packing list, delivery note; preview + reprint history (AT-18); package revision (AT-10) |
| Partial dispatch | Manager exception only (AT-6, AT-11) |
| Dispatch | Idempotent; actual qty only (AT-11, AT-12) |
| Procurement | Post-dispatch reorder events, deduped (AT-14, AT-15) |

**Exit criteria / tests:** AT-6, AT-7, AT-8, AT-9, AT-10, AT-11, AT-12, AT-14, AT-15, AT-18.

**Note:** Printing moves **here** from old Phase 06 — essential to fulfillment, not a late add-on.

---

## Phase 05 — Inbound, movements, counts, returns

**Goal:** Modules 5–11, 16; fuller procurement.

| Work item | Deliverable |
|-----------|-------------|
| Inbound ASN/receiving, QC, put-away | Modules 5–8 |
| Internal transfers, cycle count, adjustments | Modules 10–11 |
| Returns / exceptions | Module 16 |
| Reorder rules | Email + in-app (D06) |

**Tests:** Extend inbound/audit scenarios; regression AT-17.

---

## Phase 06 — 3PL commercial, carrier, mobile, analytics

**Goal:** Client SLA views, billing hooks (optional PO), carrier/ZPL, RF browser UI, advanced reporting.

| Work item | Deliverable |
|-----------|-------------|
| Owner-specific reporting | Module 19 |
| Carrier label integration | After template approval (D04) |
| ZPL / advanced printers | Optional |
| Mobile / compact RF | API-ready from Phase 03+ |

**Tests:** AT-16 full portal-style isolation; performance/load as needed.

---

## Phase 07 — Hardening (ongoing)

- Casbin policy completeness on all WMS routes  
- Full acceptance suite (AT-1–AT-20) automated where feasible  
- Cutover tooling: per-warehouse legacy → WMS with discrepancy report (D17)  
- Tamper-evidence / retention policy (D18) for enterprise

---

## Per-warehouse legacy cutover (cross-cutting)

Not a single phase — enabled after **02B** ledger trusted:

1. Dry-run import from `partfinder_inventory` → opening balance.  
2. Discrepancy report + backup.  
3. Product-owner approval.  
4. Set cutover flag → legacy **read-only** for that warehouse; WMS sole authority.

---

## Acceptance test → phase mapping (revised)

| Test | Description (short) | Phase |
|------|---------------------|-------|
| AT-1 | WMS vs Retail isolation; Excel features intact | 02A structure; 07 regression |
| AT-2 | Two owners, same SKU, separate qty | 02B |
| AT-3 | Concurrent task claim | 03 |
| AT-4 | Allocation cap | 03 |
| AT-5 | Pick: on-hand unchanged, bin/staging move | 03 |
| AT-6 | Pack mismatch blocks dispatch | 04 |
| AT-7 | Customer PN on pouch labels | 04 |
| AT-8 | Mixed pouch qty + labels | 04 |
| AT-9 | Multi-carton packing list | 04 |
| AT-10 | Package revision invalidates labels | 04 |
| AT-11 | Partial dispatch qty | 04 |
| AT-12 | Idempotent dispatch | 04 |
| AT-13 | Stock math through pick/pack | 02B + 03 + 04 |
| AT-14 | Reorder after dispatch | 04 |
| AT-15 | No spam alerts | 04 |
| AT-16 | Cross-owner security | 02A (API) + 03–04 |
| AT-17 | Audit / no silent edit | 02B+ |
| AT-18 | Print preview / scannable barcode | 04 |
| AT-19 | Offline pick retry / supervisor | 03 |
| AT-20 | Order import idempotency | 03 |

---

## Development dependencies

| Dependency | Phase | Notes |
|------------|-------|-------|
| MongoDB replica set + transactions | **02B blocker** | Unsupported → block posting; document remediation |
| Staging tenant (D15) | 02A / 02B | No mutation tests on production |
| Backend deploy (`backend.partfinder.store`) | 02A+ | WMS routes live |
| PDF library (npm) | 04 | PO approval (D14) |
| Redis | 05+ optional | Notification dedupe |

---

## Risks to existing functionality (updated)

| Risk | Mitigation |
|------|------------|
| Breaking Inventory page | No global freeze; cutover per warehouse only |
| Dual stock truth | Cutover flag + block legacy writes per warehouse |
| Master Data / Templates | No schema changes to existing collections |
| Licence / login | WMS nav additive only in 02A |

---

## Implementation order (summary)

**02A** → **02B** → **03** → **04** (pack+print+dispatch) → **05** → **06** → cutover tooling (**07**).

**Stop rule:** Do not start **02B** until **02A** gate report approved. Do not start stock mutations until **02B** transaction proof complete.