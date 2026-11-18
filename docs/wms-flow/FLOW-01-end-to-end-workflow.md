# FLOW-01 — End-to-end warehouse business workflow

**Phase:** FLOW-01 (design only — no implementation).  
**Sources:** Phase 01 architecture, Phase 02A/02B, Phase 03A implementation reports, `05-wms-development-roadmap.md`, WinUI `WmsWorkbookSections`, NestJS `wms/v1` controllers.  
**Ledger rule (non-negotiable):** Picking, packing, and **reservations** must **not** reduce physical on-hand. Only **confirmed dispatch** and **separately approved physical adjustments** post deductions per `WmsInventoryPostingService` transaction types.

---

## 1. Implementation status legend

| Tag | Meaning |
|-----|---------|
| **LIVE** | API + Workbook UI present in repo (may require staging gate for stock posts). |
| **PARTIAL** | Read-only or setup only; no operational outbound path. |
| **PLANNED** | Documented in roadmap; no production API/UI. |

---

## 2. Nineteen approved WMS modules — summary

| # | Module | Tag | Current capability (truthful) |
|---|--------|-----|----------------------------|
| 1 | Company, warehouse & 3PL client onboarding | **LIVE** | Platform org + `wmsEnabled`; tenant `wms_warehouses`, `wms_stock_owners`; operator grants. |
| 2 | SKU & warehouse master data | **LIVE** | SKU registry, identifiers, UOM refs; zones/locations; limits enforced. |
| 3 | Receiving planning & expected shipments | **LIVE** | `wms_expected_receipts` + lines; ERP refs on header. |
| 4 | Goods receiving, discrepancies, partial receipts | **LIVE** | Idempotent confirm; partial updates expected totals; discrepancy flags. |
| 5 | Quality inspection, quarantine, rejected stock | **LIVE** | QC partial accept/reject; reject stays **blocked** until supervisor disposition (no auto write-off). |
| 6 | Put-away, bin assignment, stock movements | **LIVE** | Put-away tasks; `move` post; receiving → storage. |
| 7 | Inventory availability, reservations, adjustments | **PARTIAL** | Balances + ATP (`onHand`, `reserved`, `blocked`); **no reservation API** yet; adjustments via posted txn types only. |
| 8 | Order creation, Excel import, ERP orders | **PLANNED** | ERP foundation / outbox **read** in 02B; order lines Phase 03B. |
| 9 | Employee task allocation & simultaneous picking | **PLANNED** | Grants + permissions LIVE; pick tasks Phase 03B. |
| 10 | Order staging & partial picks | **PLANNED** | Staging location type exists in master data; pick-to-staging Phase 03B. |
| 11 | Pouch verification & customer part labels | **PLANNED** | Phase 04. |
| 12 | Carton/pallet packing, verify, print | **PLANNED** | Phase 04. |
| 13 | Packing lists & delivery documentation | **PLANNED** | Phase 04 PDF suite. |
| 14 | Shipment readiness & controlled partial dispatch | **PLANNED** | Manager exception only (roadmap AT-11). |
| 15 | Dispatch confirmation & inventory deduction | **PLANNED** | **Only** outbound ledger deduct path in design. |
| 16 | ERP integration events & reconciliation | **PARTIAL** | Outbox + connectors; receiving emits events; live SAP/Oracle **not** wired. |
| 17 | Procurement alerts & replenishment | **PLANNED** | Phase 06 stub in roadmap. |
| 18 | Returns, reverse logistics, stock counts | **PLANNED** | Not in 03A scope. |
| 19 | 3PL owner management, reporting, billing | **PARTIAL** | Owners LIVE; reporting/billing **PLANNED**. |

---

## 3. Stock ledger post types (authoritative)

| Transaction type | When used (LIVE vs PLANNED) | Effect on on-hand | Effect on blocked | Effect on reserved |
|----------------|------------------------------|-------------------|-------------------|-------------------|
| `receive` | LIVE — goods receipt | ↑ receiving loc | — | — |
| `qcBlock` | LIVE — paired with receive | — | ↑ blocked | — |
| `qcRelease` | LIVE — QC accept / release | — | ↓ blocked | — |
| `move` | LIVE — put-away; PLANNED — pick to staging | transfer between locs | may move with qty | — |
| `writeOff` | LIVE — supervisor disposition; PLANNED — approved adjustments | ↓ | ↓ blocked | — |
| `reserve` | PLANNED | — | — | ↑ reserved |
| `releaseReservation` | PLANNED | — | — | ↓ reserved |
| `dispatchConfirm` | PLANNED | ↓ (ship loc / aggregated) | — | ↓ reserved |

**Design constraints:**

- **Reservation / pick / pack:** may change location, task state, `reserved`, and package hierarchy — **not** net physical on-hand at warehouse level until dispatch.
- **Pick to staging:** `move` from storage → `staging` location; conservation AT-5 (roadmap).
- **Dispatch:** atomic confirm deduct + reservation release + ERP outbox event.

---

## 4. Module workflows (normal + exception)

### 4.1 Onboarding (module 1)

**Normal:** Admin creates org entitlement → tenant URI → WMS admin creates warehouse(s), stock owner(s) → operator grants (warehouse × owner × permissions).  
**Exceptions:** License invalid → 403; warehouse limit → 409; missing grant → 403 on operations.  
**Cancellation:** Deactivate warehouse/owner (no delete of ledger history).

### 4.2 Master data (module 2)

**Normal:** Register SKU (canonical `skuId`), UOM, identifiers; define location paths (receiving, storage, staging, dispatch).  
**Exceptions:** Duplicate SKU code; invalid location type; merge blocked when open balances exist (guards in sku service).  
**Authorization:** Admin write; operators read scoped lists only.

### 4.3 Expected shipments (module 3)

**Normal:** Create expected receipt (owner, warehouse, PO/ERP refs) → add lines (SKU, qty) → status `open`.  
**Exceptions:** Wrong owner scope → 403; closed expected → no new lines.

### 4.4 Goods receiving (module 4)

**Normal:** Confirm receipt with idempotency key → lines post `receive` + `qcBlock` → expected `partially_received` / `received`.  
**Exceptions:** Gate closed → 403; duplicate idempotency → success `duplicate: true`; over-delivery flags → discrepancy worksheet.  
**Partial:** Multiple confirms until expected satisfied.

### 4.5 Quality control (module 5)

**Normal:** Partial accept/reject → `qcRelease` on accepted portion; rejected qty remains **on-hand blocked**.  
**Exceptions:** Reject **does not** auto write-off (03A.1); supervisor **disposition** required for write-off/return.  
**Quarantine:** Status hold; stock stays blocked.

### 4.6 Put-away (module 6)

**Normal:** Open task from QC release → complete with target bin → `move` txn.  
**Exceptions:** Invalid bin; qty mismatch; idempotent complete key.

### 4.7 Availability & reservations (module 7)

**LIVE:** Query balances; ATP = onHand − reserved − blocked (read service).  
**PLANNED:** Reserve against order line (increments `reserved` only); cycle count adjustment with approval workflow.

### 4.8–4.19 Outbound & corporate (modules 8–19)

Detailed state machines in **FLOW-03** and diagrams in **FLOW-05**. Common pattern:

1. Order imported (idempotent external id).  
2. Reserve stock (soft commit).  
3. Pick tasks claimed atomically → confirm pick → **move to staging** (no net deduct).  
4. Pack verify pouch/carton → print labels (preview/history).  
5. Shipment ready check → dispatch confirm → **deduct** + ERP event.  
6. Failed ERP → retry queue + supervisor reconciliation screen.

---

## 5. Cross-cutting concerns

| Concern | Design |
|---------|--------|
| **3PL isolation** | Every txn carries `ownerId`; scope enforced in guard + list APIs. |
| **Idempotency** | Goods receipt, put-away complete, future dispatch confirm. |
| **Retries** | Safe replays return duplicate flag; ERP outbox at-least-once with idempotent consumers. |
| **Reversal** | No silent delete of posted rows; corrective **adjustment** txn with approval (PLANNED formal workflow). |
| **Staging gate** | Receiving mutations require preflight evidence + `WMS_DEPLOYMENT_TIER` + staging posting flag. |

---

## 6. References

- Backend receiving: `WmsReceivingService`, `wms-phase03a.controller.ts`  
- Posting: `WmsInventoryPostingService`  
- Session scope: `WmsSessionGuard`, `wms-setup.service` list filters  
- UX screen list: `docs/wms-ux/UX-03-screen-blueprints.md`

---

*Next:* [FLOW-02-user-roles-and-journeys.md](./FLOW-02-user-roles-and-journeys.md), [FLOW-05-visual-flowcharts.md](./FLOW-05-visual-flowcharts.md)