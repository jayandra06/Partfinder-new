# 03 — WMS Architecture (Phase 01 — approved)

**Status:** Approved product architecture (2026-11-16, reconciled). Implementation not started until Phase 02A is authorized.  
**Inputs:** Functional blueprint v1.0  
**Cross-ref:** `07-blueprint-traceability.md`; **decisions:** `06-wms-risks-and-questions.md` (authoritative)

---

## Approved principles

1. **Separate Warehouse Management Workbook** — distinct from legacy Inventory and future Retail (Retail out of scope).
2. **Mandatory WMS API (mutations + scoped reads)** — **All new WMS inventory mutations** flow through authenticated NestJS. **Operational reads** (balances, orders, tasks, packages) are served through the same API with **tenant, warehouse, and stock-owner** permission enforcement (D01, D01b). WinUI may hold **read-only caches** for performance; caches are not stock truth and must not write tenant Mongo for WMS.
3. **Transactional backend** — stock ledger postings require **multi-document atomic commits** where a single business operation spans multiple records. **No** unsafe compensating-update fallback for inventory posting. Single-document atomic operations (e.g. task claim via compare-and-set) remain acceptable.
4. **3PL from Phase 02A** — multiple **stock owners** per subscribing organization; `ownerId` mandatory on balances, ledger, reservations, orders, packages, and shipments; **server-side** owner access enforcement.
5. **Legacy coexistence** — **Preserve** existing PartFinder **Inventory** page and `partfinder_inventory` writes for non-migrated warehouses. Per-warehouse **migration and cutover** controls only (D17, D28). No global disable of legacy inventory editing.
6. **Fulfillment invariants** — pick/pack change location and fulfillment substatus only; **do not** reduce total physical on-hand until **dispatch** of verified shipped quantities. Mandatory pack verification before seal; block full dispatch on incomplete orders unless approved partial exception.
7. **Printing in packing phase** — first **PDF** print set ships with packing/dispatch (pouch, carton, pallet, packing list, delivery note, preview, reprint history). Carrier/ZPL later.
8. **Lot and serial** — optional dimensions on **stock balances, movements, reservations, and package contents** (not pack-document-only).
9. **Procurement** — dispatch posts auditable movements and updates authorized replenishment views/alerts; no duplicate alerts on retry.

---

## 1. High-level architecture (mandatory API)

```text
┌─────────────────────────────────────────────────────────────┐
│  PartFinder WinUI                                           │
│  ┌────────────────┐  ┌─────────────────────────────────────┐│
│  │ Existing       │  │ WMS Workbook (new)                  ││
│  │ Master Data,   │  │ Setup, Orders, Pick, Pack+Print,    ││
│  │ Templates,     │  │ Dispatch, Ledger views, Reports     ││
│  │ Legacy Inv.*   │  └──────────────────┬──────────────────┘│
│  └────────────────┘                     │                     │
│           * unchanged until per-wh cutover                     │
│                     WmsApiClient (reads + writes via NestJS)   │
│                     optional local read cache (invalidate TTL) │
└─────────────────────────────┬───────────────────────────────┘
                              │ HTTPS + session JWT
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  PartFinder-Backend — NestJS `/api/wms/v1/*`                │
│  • Resolve tenant from verified token (not client orgCode)    │
│  • Connect to tenant Mongo via server-managed orgDatabaseUri │
│  • Enforce Casbin/capabilities + owner + warehouse scope    │
│  • Ledger, reservations, tasks, packages, print jobs        │
└─────────────────────────────┬───────────────────────────────┘
                              │
                              ▼
                    Tenant MongoDB (per organization)
                    Additive `wms_*` collections only
```

**Explicitly rejected for WMS:** WinUI → `MongoInventoryService`-style direct Mongo mutations for warehouse stock, orders, or ledger.

---

## 2. Authentication and tenancy

| Rule | Detail |
|------|--------|
| Identity | Verified **session/invite JWT** from existing setup auth (`setup.service.ts`, `AdminSessionStore` / refresh flow) |
| Tenant resolution | Server maps user → **organization** → `orgDatabaseUri`; never trust client-supplied `orgCode` or `ownerId` without server authorization |
| Scope | Every WMS request carries implied or explicit **warehouseId**; stock queries filter by authorized **ownerId** set |
| Desktop | `BackendApiClient` / new `WmsApiClient` pattern; same licence API base URL |
| Casbin | Policies **enforced on WMS routes** in Phase 02A+ (sync alone is insufficient — see blockers) |
| Subscription | **WMS entitlement** on every WMS route: org licence active + WMS feature enabled + applicable limits (D25, B7) — extend `organizations.service.ts` `verifyLicense` pattern |

---

## 3. Quantity states (no double counting)

Definitions are **orthogonal** — never sum “substate” columns into physical on-hand.

| Term | Meaning | Counted in physical on-hand? |
|------|---------|------------------------------|
| **Physical on-hand** | Sum of qty in all custody locations (storage, staging, sealed cartons on dock) for owner/SKU/(lot/serial) | **Yes** (definition) |
| **Committed** | Qty tied to confirmed customer demand (order lines) not yet fully shipped | Substate only |
| **Reserved** | Subset of committed with balance allocation against specific buckets | Substate only |
| **Picked** | Physically moved to order staging / pick face (location transfer) | **Inside** on-hand (bin ↓, staging ↑) |
| **Packed** | Qty verified inside a package (pouch/carton); package may be **sealed** while order still open (D27) | **Inside** on-hand until dispatch |
| **Blocked** | QC hold, quarantine, damage — not available | **Inside** on-hand; reduces **available** |
| **Available (ATP)** | Policy-derived sellable/pickable qty | **Not** physical; `f(onHand, blocked, reserved)` — do **not** subtract picked/packed again if already in reserved |

**Conservation:** Pick and pack **do not** reduce total physical on-hand. **Dispatch** reduces on-hand by verified shipped quantity only (D23).

| Stage | Effect on total on-hand | Effect on available / committed |
|-------|-------------------------|-------------------------------|
| Reserve | Unchanged | Available ↓; committed ↑ |
| Pick to staging | Unchanged (bin ↓, staging ↑) | Committed holds units |
| Pack verify + seal carton | Unchanged | Link qty to package ID; carton may seal independently |
| Dispatch | **On-hand ↓** by shipped D only | Release allocations |

**Order completion:** Block order **`closed` / fully dispatched`** until line quantities reconcile (ordered vs verified packed vs dispatched) or **approved partial** exception (P03).

Ledger events (immutable; correct via reversal): `RECEIPT_POSTED`, `ALLOCATED`, `PICK_CONFIRMED`, `STAGING_TRANSFER`, `PACK_CONFIRMED`, `DISPATCH_POSTED`, `COUNT_ADJUSTMENT`, `PACK_REVERSED`, etc.

**Lot / serial (D22):** When enabled for a SKU, carry `lotId` / `serialId` through receiving → balances → reservations → pick confirm → package contents → dispatch lines.

**Schema:** Location-level balances in `04-wms-database-proposal.md` §0; key **owner × warehouse × location × skuKey × optional lot × serial**.

---

## 4. Backend module structure (`PartFinder-Backend/src/wms/`)

| Submodule | Responsibility | First phase |
|-----------|----------------|-------------|
| `setup` | Warehouses, zones, locations, owners | **02A** |
| `auth` | Tenant connection, permission guards, **WMS entitlement** | **02A** |
| `ledger` | Balances + append-only transactions | **02B** |
| `reservations` | Order allocations | **03** |
| `outbound` | Orders, import idempotency | **03** |
| `tasks` | Pick claim (single-doc atomic) | **03** |
| `packing` | Packages, verification, hierarchy | **04** |
| `print` | PDF generation, versions, reprint audit | **04** |
| `dispatch` | Shipment confirm, idempotent deduct | **04** |
| `procurement` | Reorder evaluation, deduped alerts | **04** (basic); **05** (full) |
| `inbound` | ASN, receiving, QC, put-away | **05** |

---

## 5. WinUI workbook structure

| Section | Phase | UX reuse |
|---------|-------|----------|
| WMS setup (warehouses, owners, locations, access) | 02A | Master Data grid patterns |
| Ledger / balances (read-only API) | 02B | TemplateData-style filters |
| Orders + import queue | 03 | — |
| Pick tasks | 03 | Compact scan-friendly panel |
| Pack + **print** (pouch/carton/pallet/list/delivery note) | 04 | QR label patterns from `QrCodeManagerViewModel` |
| Dispatch confirm | 04 | — |
| Reports / procurement view | 04–05 | Dashboard patterns |

**Navigation:** Independent **Warehouse Management Workbook** entry (`AppPage`); legacy **Inventory** page remains until per-warehouse cutover (`D16`, `D17`).

---

## 6. Concurrency and transactions

| Operation | Mechanism |
|-----------|-----------|
| Ledger post + balance + reservation | **Mongo multi-document transaction** (required) |
| Task claim | Single-document atomic update (`status` + `version`) |
| Dispatch confirm | Transaction + **idempotency key** (no double deduct) |
| Unsupported Mongo topology | **Block posting**; document remediation — no compensating saga for stock |

**Pre-implementation gate:** Prove transaction support on **staging tenant** replica set (`06-wms-risks-and-questions.md` blocker B1).

---

## 7. Legacy inventory cutover

| State | Behavior |
|-------|----------|
| Warehouse **not migrated** | Existing `InventoryPage` + `partfinder_inventory` unchanged |
| **Cutover approved** | Import/mirror opening balances → WMS ledger; set `wms_warehouse_cutover` flag; legacy UI **read-only or hidden** for that warehouse; API rejects dual writes |
| Global | No automatic sync between legacy collection and WMS balances |

---

## 8. Packing verification, seal rules, dispatch gate

- Record **actual picked** and **actual packed** quantities per line/task/package.
- **Package-level seal:** a **carton** (or pallet) may be **sealed** after its own contents are verified, **before** the entire order is complete (D27).
- **Order-level close:** prevent **full order completion** or “fully dispatched” unless quantities **reconcile** across lines (or approved partial per P03).
- Hierarchy: order → shipment → pallet → carton → pouch → items.
- **Customer-requested part number** on **order line** for pouch PDF (not inferred from SKU master).
- **Live dispatch gate (D26):** no production dispatch deduction until mandatory pack verification and **basic PDF** pouch/carton/packing-list printing exist in Phase 04.
- Label **versions** invalidated when package reopened (`AT-10`).

---

## 9. Print architecture (Phase 04, not deferred)

| Document | Format (v1) |
|----------|----------------|
| Pouch label | PDF, customer PN prominent, package ID barcode (Code 128) |
| Carton / pallet label | PDF, unique package ID |
| Packing list | PDF from **verified** package contents |
| Delivery note | PDF post-dispatch approval |
| Preview / reprint | Server-stored artifact version + audit |

**PDF v1 (D04); ZPL: proposed default P01** — carrier-specific formats after validation. Dependency approval required (`D14`).

---

## 10. Security summary

- Owner-scoped data: server filters all list/get mutations.
- Separation of duties: dispatch vs pack vs pick roles.
- WMS audit: **server-side append-only** (`D18`); client `MongoAuditService` not authoritative for stock.
- During coexistence: users cannot bypass WMS for cutover warehouses via legacy Mongo paths.

---

## 11. Dependencies (by phase)

| Phase | Dependency |
|-------|------------|
| 02A | Nest `wms` module, JWT guards, tenant Mongo connector proof |
| 02B | Mongo **transactions** verified |
| 04 | PDF library (npm) — **approved before install** |
| 05 | SMTP for procurement (existing), optional Redis for notification dedupe |
| 06+ | Elasticsearch optional; carrier APIs optional |

---

## 12. Related documents

| Doc | Role |
|-----|------|
| `04-wms-database-proposal.md` | Entities — to be revised for lot/serial and balance invariants |
| `05-wms-development-roadmap.md` | Phases 02A–06 and test mapping |
| `06-wms-risks-and-questions.md` | **Authoritative** decision register (D01–D28, P01–P04), blockers, readiness checklist |
| `07-blueprint-traceability.md` | Requirement → code → phase |
| `08-architecture-decisions.md` | Index to decision register (non-authoritative) |