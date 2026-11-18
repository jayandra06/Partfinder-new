# Phase 03A — Implementation Report

**Scope:** Goods receiving, quality control, put-away, operator permissions, staging transaction gate, ERP-ready receipt metadata, Warehouse Workbook receiving worksheets (read-centric UI).  
**Status:** Backend APIs complete and unit-tested; WinUI lists and navigation operational; **staging end-to-end stock workflow not evidenced in this workspace.**  
**Production posting:** OFF (`WMS_LIVE_STOCK_POSTING` ≠ `true`).  
**Receiving stock mutations:** OFF unless `WMS_STAGING_STOCK_POSTING=true` **and** successful MongoDB transaction preflight evidence exists for the tenant.

---

## 1. Mandatory technical gate

| Check | Result in this workspace |
|-------|---------------------------|
| MongoDB transaction preflight on named staging tenant | **Not executed** — no staging MongoDB URI available in dev; evidence collection not populated. |
| Concurrent posting / rollback / idempotency / owner-isolation DB tests | **Not implemented** as automated integration suites; existing **unit** coverage for staging gate, posting guards, scope, session guard (30 WMS Jest tests). |
| Phase 02B.1 posting atomicity (balances + ledger + outbox in txn) | **Unchanged**; receiving calls `WmsInventoryPostingService.post()` for receive, `qcBlock`, `qcRelease`, `writeOff`, and `move`. |
| Recorded test evidence | Only **build/test CLI output** below; no `wms_staging_gate_evidence` document from a real tenant. |

**Gate behaviour (fail-closed):**

1. `WMS_STAGING_STOCK_POSTING` must be `true` (see `wms-staging-stock.util.ts`).
2. Latest row in `wms_staging_gate_evidence` must have `supported: true`.
3. Admins record preflight via `POST /api/wms/v1/staging-gate/preflight` (runs `WmsDiagnosticsService.probeTransactions`).

Until both are satisfied, `confirmGoodsReceipt`, QC decisions that post stock, and `completePutaway` throw `ForbiddenException`.

**How operators unblock staging (outside this repo):**

```http
POST /api/wms/v1/staging-gate/preflight
Authorization: Bearer <jwt>
Content-Type: application/json

{ "tenantLabel": "staging-demo-111111" }
```

Set `WMS_STAGING_STOCK_POSTING=true` on the **staging** NestJS host only.

---

## 2. Backend — new / modified files

| File | Role |
|------|------|
| `wms-phase03a.controller.ts` | Receiving + staging-gate HTTP API |
| `services/wms-receiving.service.ts` | Expected receipts, confirm receipt, QC, put-away, audit |
| `services/wms-staging-gate.service.ts` | Preflight evidence + `assertReceivingStockMutationsAllowed` |
| `dto/wms-receiving.dto.ts` | Validation for 03A payloads |
| `wms-operator-permissions.util.ts` | Receiving / QC / put-away / supervise assertions |
| `wms-stock-transaction.types.ts` | Added `qcBlock`, `qcRelease` transaction types |
| `services/wms-inventory-posting.service.ts` | `blockedQty` on balances; handlers for QC block/release |
| `wms-tenant-collections.service.ts` | Phase 03A collections + `ensurePhase03AIndexes()` |
| `guards/wms-session.guard.ts` | Loads `wms_operator_grants.permissions` into `operatorPermissions` |
| `wms.types.ts` | Collection names, `WmsOperatorGrants`, context fields |
| `wms-staging-gate.service.spec.ts` | Missing-evidence forbidden path |
| `wms-phase02.controller.ts` | Catalog `phase: '03A'` |

---

## 3. MongoDB collections (per tenant)

| Collection | Purpose |
|------------|---------|
| `wms_expected_receipts` | ASN / expected lines, ERP refs |
| `wms_goods_receipts` | Posted receipts; unique `idempotencyKey` |
| `wms_receipt_lines` | Lots, serials, discrepancy flags, QC/put-away status |
| `wms_qc_inspections` | QC workflow records |
| `wms_putaway_tasks` | Open/completed moves receiving → storage |
| `wms_receiving_audit_log` | Append-only receiving audit |
| `wms_staging_gate_evidence` | Transaction preflight snapshots |

Legacy `partfinder_inventory` remains read-only for WMS.

---

## 4. API endpoints (`/api/wms/v1`, JWT + `WmsSessionGuard`)

| Method | Path | Permission / notes |
|--------|------|-------------------|
| POST | `staging-gate/preflight` | WMS admin write |
| GET | `staging-gate/latest` | Any scoped session |
| POST | `receiving/expected` | Receiving operator |
| GET | `receiving/expected` | Scoped warehouse filter |
| POST | `receiving/expected/:id/lines` | Receiving operator |
| POST | `receiving/goods-receipts` | Receiving; **stock posts** when gate open |
| GET | `receiving/goods-receipts` | List |
| GET | `receiving/goods-receipts/:id/lines` | Line detail |
| POST | `receiving/qc/:receiptLineId` | QC / supervise |
| GET | `receiving/qc` | Optional `status` filter |
| GET | `receiving/putaway` | Open tasks |
| POST | `receiving/putaway/:putawayTaskId/complete` | Put-away; **move** post |
| GET | `receiving/discrepancies` | Short/over/damage |
| GET | `receiving/history` | Audit log |

### Workflow (backend)

1. **Expected receipt** — metadata: `ownerId`, `warehouseId`, `externalPoId`, `supplierDeliveryNote`, `erpConnectorId`, `supplierId`.
2. **Confirm goods receipt** — idempotent on `idempotencyKey`; per line: `receive` + `qcBlock` (stock not ATP until QC release); creates `pending` QC inspection.
3. **QC** — `accepted` / `released`: `qcRelease` + put-away task; `rejected`: `writeOff` + ERP event; `quarantined`: status only (remains blocked).
4. **Put-away** — `move` from receiving location to `toLocationId`; task completed; line `putawayStatus` updated.

ERP: `emitErpEvent: true` on receive, QC release/reject, put-away move (outbox in same txn as posting when staging/live allowed).

---

## 5. Operator permissions

Grants stored in tenant `wms_operator_grants` document (`permissions` object):

| Flag | Maps to role intent |
|------|---------------------|
| `receiving` | Receiving Operator |
| `qc` | Quality Control Operator |
| `putaway` | Put-away Operator |
| `supervise` | Warehouse Supervisor (QC without separate QC flag) |
| `isWmsAdmin` | Warehouse Administrator (all flags via `adminOperatorGrants()`) |

Warehouse and stock-owner scope from existing grant lists; `assertOwnerAccess` / `assertWarehouseAccess` on every mutating path.

---

## 6. WinUI — Warehouse Workbook

### Navigation (operational 03A group)

Expected Receipts, Goods Receiving, Received Item Details, Quality Control, Put-away Tasks, Location Assignment (put-away list reuse), Receiving History, Discrepancies.

### Roadmap labels (updated)

| Phase | Workbook placeholder |
|-------|----------------------|
| 03A | Receiving group (live) |
| 03B | Orders, Picking |
| 04 | Packing, Printing |
| 05 | Dispatch, Procurement |
| 06 | Reports |
| 07 | Administration subtitle |

`WmsFutureModuleRegistry.cs` phase tags aligned for order/pick/pack/dispatch/procurement.

### Implementation level

- **Done:** Section switching, list reload per warehouse, gate summary text, export button (SKU worksheet CSV via ClosedXML path in `WmsWorksheetExportService`).
- **Not done:** Excel-grade grids (resize, sort, filter, keyboard nav, bulk paste/import, virtualization) across receiving worksheets; POST/create flows not wired in `WmsApiClient` (lists only); rich editors for expected receipt / confirm receipt / QC / put-away from WinUI.

Posted ledger rows remain read-only in Movement Ledger (unchanged from 02B.2).

---

## 7. ERP integration (stubs only)

Preserved on receipts and posting `refs` / outbox payloads:

- External PO, delivery note, supplier id, connector id  
- Owner + SKU ids  
- Received quantity, QC outcome  
- Idempotent receipt keys and transaction references  

No SAP/Oracle connectors in this phase.

---

## 8. Test evidence (this run)

| Suite | Command | Result |
|-------|---------|--------|
| NestJS build | `npm run build` (PartFinder-Backend) | **Pass** |
| WMS unit tests | `npm test -- --testPathPatterns=wms` | **30 passed** (10 suites) |
| WinUI build | `dotnet build PartFinder/PartFinder.csproj -c Debug -p:Platform=x64` | **Pass** (pre-existing warnings) |
| WinUI tests | `dotnet test PartFinder.Tests/PartFinder.Tests.csproj` | **23 passed** |

**Not run:** Staging workflow Expected → Receive → QC → Put-away → balance/ledger on a real tenant; duplicate-receipt hammer test; cross-owner receive without authorization; rejected stock ATP exclusion (logic implemented via `blockedQty` / `qcBlock` but not integration-proven here).

---

## 9. Known gaps / risks

1. **Staging gate** — Receiving mutations will 403 until preflight + env flag; document above for ops.
2. **No `wms-receiving.service.spec.ts`** — service logic covered indirectly; add integration tests in Phase 03B hardening.
3. **Partial receipts** — Multiple confirmations against one expected receipt are possible at API level; UI does not guide partial state machine.
4. **Quarantined** — Keeps QC block; no separate “release from quarantine” posting path beyond `released` status.
5. **WinUI** — ListViews only; operational data entry still via API/Postman until POST client methods and grids land.
6. **Excel UX** — Section 6 of phase spec largely **deferred** (same as 02B.2 baseline + SKU export).
7. **Phase 07** — Label on Administration only; no separate enterprise worksheet pack.

---

## 10. Readiness for Phase 03B

| Area | Ready? |
|------|--------|
| Receiving/QC/put-away APIs | Yes — pending staging validation |
| Stock ledger integration | Yes — types and posting hooks in place |
| WinUI operational receiving | **Partial** — read lists; needs POST UX + grid parity |
| Staging sign-off | **No** — gate evidence and E2E checklist required before production-adjacent pilots |
| Orders / reservations / picking | **Not started** (authorized next: 03B) |

**Explicitly out of scope (unchanged):** order picking, packing, dispatch, live ERP, production `WMS_LIVE_STOCK_POSTING`.

---

*Report generated at Phase 03A code complete + local build verification. Update the gate table after staging preflight and E2E run.*