# Phase 03A.1 — Implementation Report

**Status:** **INCOMPLETE** relative to the Phase 03A.1 acceptance condition (full WinUI workflow on an authorized staging tenant with recorded evidence).  
**Reason:** No authorized staging MongoDB tenant or live WinUI manual run was available in this workspace.  
**Code status:** Backend QC corrections, atomic receipt posting, partial receiving, operator grants API, WinUI POST workflows, and disposable integration tests are implemented and build clean.

**Production posting:** OFF. **Live ERP:** Not connected.

---

## 1. WinUI screens and supported user actions

| Worksheet | User actions (via API) |
|-----------|------------------------|
| Expected receipts | Draft lines (SKU, qty, UOM), Excel import (xlsx), save expected receipt + lines, list/select expected receipts |
| Goods receiving | Draft receipt lines, confirm physical receipt (idempotent key per submit), list receipts |
| Received item details | Read-only lines for selected goods receipt |
| Quality control | Partial accept/reject qty, apply QC, list inspections |
| Put-away tasks / Location assignment | Select task, target storage location, complete put-away |
| Receiving history / Discrepancies | Read-only lists |
| Administration | Operator grant list/save (admin), session profile drives enablement |

**Wired APIs:** `WmsApiClient` POST for expected, goods receipt, QC, put-away, operator grants; `GET /wms/v1/session/profile`.

**Excel UX (incremental):** Editable draft rows, warehouse/owner/location combo boxes, expected-line Excel import, export (SKU unchanged), list search via existing workbook search, refresh per worksheet. **Not yet:** dedicated column resize/sort UI control, clipboard paste handler, grid virtualization component shared with future 03B worksheets.

**Manually verified on staging WinUI:** **Not executed** (blocker).

---

## 2. Backend changes and API connections

| Change | Detail |
|--------|--------|
| Atomic goods receipt | Single MongoDB transaction: receipt docs + `receive` + `qcBlock` per line via `executeCommandsInSession` |
| Partial expected receipt | `qtyReceivedTotal`, status `partially_received` / `complete` / `closed_with_exception`; over-delivery requires `overDelivery` flag |
| QC correction | **No automatic write-off on reject**; partial `qtyAccepted` / `qtyRejected`; `qcRelease` only for accepted qty; rejected stays blocked on-hand |
| Physical disposition | `POST /receiving/disposition/:receiptLineId` → explicit `writeOff` (supervisor/admin only) with reason |
| Operator grants | `GET/POST /operator-grants`, `POST /operator-grants/:email/remove` |
| Session profile | `GET /session/profile` for WinUI permission flags |

---

## 3. QC corrections (accounting)

| Rule | Implementation |
|------|----------------|
| Receipt increases physical on-hand | `receive` transaction |
| Pending QC blocked from ATP | `qcBlock` in **same transaction** as receive |
| Accepted released for put-away | `qcRelease` for `qtyAccepted` only |
| Rejected remains on-hand, blocked | No ledger removal; blocked qty unchanged for rejected portion |
| Quarantined | Status only; stock stays blocked |
| Write-off | Separate disposition endpoint; not tied to QC reject |

---

## 4. Receiving / put-away accounting (integration test scenario)

Disposable replica-set test (`wms-receiving-workflow.integration.spec.ts`) — **not staging evidence**:

- Expected 100 → receive 60 → partial QC 55 accept / 5 reject → put-away 55 to storage.
- **Result:** receiving location on-hand **5**, blocked **5**; storage on-hand **55**, blocked **0**; expected status `partially_received`; duplicate idempotency key does not double stock.
- Reject-only QC: on-hand ≥ 3, blocked ≥ 3 (no write-off).
- Cross-owner access denied (`ForbiddenException`).

---

## 5. Excel-style grid functionality

| Feature | State |
|---------|--------|
| Editable drafts | Expected + goods receipt line drafts |
| Dropdowns | Owner, warehouse, receiving/storage locations |
| Multi-line documents | Draft collections + save commands |
| Excel import | Expected lines (columns: SKU, Qty, UOM) |
| Excel export | SKU registry only (receiving export TODO) |
| Search / refresh | Workbook search + Refresh worksheet |
| Pagination / virtualization | Inventory worksheets only (balances/ledger) |
| Posted stock immutability | Ledger/balance views read-only |

---

## 6. MongoDB staging preflight evidence

| Item | Result |
|------|--------|
| Named staging tenant preflight | **Not run** — no staging URI in workspace |
| `wms_staging_gate_evidence` on staging | **Not recorded** |
| Disposable replSet integration tests | **Passed** (3 tests) — see §4 |

---

## 7. End-to-end test evidence

| Test | Result |
|------|--------|
| Backend `npm run build` | **Pass** |
| `jest --testPathPatterns=wms` | **33 passed** (11 suites, incl. integration) |
| WinUI `dotnet build` x64 | **Pass** |
| `PartFinder.Tests` | **23 passed** |
| WinUI staging scenario (100 filters) | **Not executed** |
| Repeat idempotency on staging UI | **Not executed** |
| Client B unauthorized on staging UI | **Not executed** |

---

## 8. Employee permission tests

| Check | Evidence |
|-------|----------|
| Integration: Client B cannot create receipt for Client A owner | **Pass** (automated) |
| Operator grants CRUD | API implemented; WinUI admin save |
| Session profile gating buttons | Implemented (`CanOperateReceiving/Qc/Putaway`) |
| Manual multi-user staging test | **Not executed** |

---

## 9. Regression (existing PartFinder)

| Area | Result |
|------|--------|
| Retail / Master Data code paths | **No intentional changes** |
| Legacy `partfinder_inventory` | **Still not written by WMS posting** |
| WinUI tests | **23/23 pass** |

---

## 10. Remaining bugs and blockers

1. **Acceptance blocker:** Staging tenant + `WMS_STAGING_STOCK_POSTING` + preflight evidence + WinUI manual E2E not performed.
2. **Excel parity:** Column resize, sort chrome, clipboard paste, receiving worksheet export — partial.
3. **Operator grant UI:** Simplified (email + default permissions); no per-warehouse/owner picker matrix.
4. **Disposition:** API exists; WinUI screen not added (supervisor uses API or future UI).
5. **Jest:** Integration suite may need `--forceExit` due to open Mongo handles (teardown follow-up).

---

## 11. Phase 03B readiness recommendation

**Do not authorize Phase 03B** until:

1. Staging gate evidence recorded on the **named staging tenant** (not replSet tests).
2. WinUI E2E for the 100-filter scenario passes with ledger/balance reconciliation screenshots or exported data.
3. Receiving Excel UX gaps closed or explicitly deferred with sign-off.

**Technical foundation for 03B:** Receiving/QC/put-away posting patterns and atomic sessions are in place; operator grants and session profile support scoped operators.

---

## Operator checklist (staging — outside repo)

1. Set `WMS_STAGING_STOCK_POSTING=true` on staging API only.
2. `POST /api/wms/v1/staging-gate/preflight` with `{ "tenantLabel": "<name>" }`.
3. Assign `wms_operator_grants` for warehouse staff.
4. Run WinUI workflow: Expected → Receive 60 → QC 55/5 → Put-away → verify balances/ledger.
5. Append evidence to this document §6–§7 and re-issue report as **COMPLETE**.

*Report version: Phase 03A.1 code complete; acceptance condition **not** met in this environment.*