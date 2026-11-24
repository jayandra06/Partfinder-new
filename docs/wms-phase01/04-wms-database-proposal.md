# 04 — WMS Database Proposal (Additive)

**Status:** Revised 2026-11-16 per `PartFinder_WMS_Phase01_Architecture_Decisions.md` and `03-wms-architecture.md`.  
**Constraint:** Do not alter or drop existing collections. New collections only. No breaking migrations on `partfinder_inventory` without per-warehouse cutover plan (D17).

**Location:** Tenant MongoDB (same URI as today). Platform Mongo remains for org licence and **WMS entitlement** fields (D25). WMS **mutations** and scoped **reads** go through NestJS (D01, D01b).

---

## 0. Quantity states (approved — see `03-wms-architecture.md` §3)

| Term | Stored where | Notes |
|------|--------------|--------|
| Physical on-hand | `wms_stock_balances.onHand` per **location** | Sum locations = total physical custody |
| Blocked | Balance flag or location type `quarantine` | Reduces ATP, still in on-hand |
| Reserved | `wms_reservations` + optional balance `reserved` | Fulfillment substate |
| Committed | `wms_order_lines` qty confirmed | Derived; not added to on-hand |
| Picked | Staging locations + line `qtyPicked` | Transfer only |
| Packed | `wms_package_contents` + line `qtyPacked` | Package `status=sealed` per carton (D27) |
| Available | **Computed** server-side | Never persisted as extra physical qty |

**Double-counting forbidden:** Do not add reserved + picked + packed on top of on-hand. Do not use a global `pickedStaging` column summed with bin balances.

**Cutover:** `wms_warehouses.wmsAuthoritativeSince` (or `wms_warehouse_cutover`) — legacy `partfinder_inventory` remains editable for **other** warehouses (D17).

**SKU gate (D19 / B6):** Do not create owner-specific balances until `skuKey` mapping from master data is signed off.

**Platform (proposed schema extension for B7):** `organizations.wmsEnabled`, `maxWmsWarehouses`, `maxStockOwners` — enforce in WMS guard alongside existing `status`, `validity`, `maxUsers`.

---

## 1. Core reference data

### `wms_warehouses`

| Field | Type | Notes |
|-------|------|-------|
| `_id` | ObjectId | |
| `code` | string | Unique per org |
| `name` | string | |
| `timeZone` | string | |
| `isActive` | bool | |
| `createdAt` / `updatedAt` | date | |

### `wms_locations`

| Field | Type | Notes |
|-------|------|-------|
| `warehouseId` | ObjectId | |
| `path` | string | e.g. `A-01-02-03` |
| `type` | enum | `receiving`, `storage`, `staging`, `dispatch` |
| `isPickable` | bool | |

### `wms_stock_owners` (3PL clients)

| Field | Type | Notes |
|-------|------|-------|
| `code` | string | Client / company code |
| `name` | string | |
| `notificationEmails` | string[] | Procurement alerts |

### `wms_skus` (optional link layer)

| Field | Type | Notes |
|-------|------|-------|
| `partId` / `masterDataRowId` | string | Link to existing catalogue |
| `ownerId` | ObjectId | Stock ownership |
| `barcode` | string | |

*Alternative:* Derive SKU from master data + owner without duplicate table — **PO decision**.

---

## 2. Inventory ledger (source of truth)

### `wms_stock_balances`

Compound unique key: `{ warehouseId, locationId, ownerId, skuKey, lotId?, serialId? }` (lot/serial omitted when not tracked)

| Field | Type |
|-------|------|
| `onHand` | decimal — physical qty **at this location** (staging counts here, not double-counted) |
| `reserved` | decimal — committed to orders (fulfillment substate) |
| `version` | int (optimistic lock) |

*Note:* Prefer **location-level** balances (bin vs staging) over a separate `pickedStaging` column that could be mis-summed. If a denormalized `pickedStaging` rollup exists for UI, it must be a **view** derived from location type = `staging`, not summed with bin `onHand` for totals.

### `wms_stock_transactions` (append-only)

| Field | Type | Notes |
|-------|------|-------|
| `txnId` | string | ULID |
| `type` | enum | `receive`, `move`, `reserve`, `unreserve`, `pick`, `pack`, `dispatch`, `adjust`, `reversal` |
| `refs` | object | orderId, taskId, packageId |
| `ownerId` | ObjectId | Mandatory |
| `lines` | array | skuKey, qtyDelta, fromLoc, toLoc, **lotId?**, **serialId?** |
| `createdBy` | string | email |
| `createdAt` | date | |
| `reversalOf` | string? | |

**Replaces** cell-level qty edits over time; legacy `partfinder_inventory` synced or deprecated.

---

## 3. Inbound

### `wms_inbound_shipments` / `wms_inbound_lines`

Expected receipts (ASN). Status: `planned` → `receiving` → `closed`.

### `wms_receipts` / `wms_receipt_lines`

Posted from receiving worksheet; creates `receive` transactions.

---

## 4. Outbound

### `wms_orders`

| Field | Notes |
|-------|-------|
| `orderNumber` | |
| `ownerId` | Client company |
| `warehouseId` | |
| `status` | `draft`, `confirmed`, `picking`, `packing`, `ready_dispatch`, `partial_dispatch`, `closed` |
| `reconciliationStatus` | `open`, `reconciled`, `partial_approved` — required before `closed` (D27) |
| `allowPartialDispatch` | bool + approval ref (P03) |

### `wms_order_lines`

`skuKey`, `qtyOrdered`, `qtyReserved`, `qtyPicked`, `qtyPacked`, `qtyDispatched`, `customerPartNumber` (pouch label), `lotId?`, `serialConstraints?`.

### `wms_reservations`

Tie line to balance buckets when order confirmed. Fields include `ownerId`, `warehouseId`, `skuKey`, `qty`, `locationId?`, **`lotId?`**, **`serialId?`**.

---

## 5. Picking and tasks

### `wms_pick_waves` (optional)

Group orders for combined picking.

### `wms_pick_tasks`

| Field | Notes |
|-------|-------|
| `orderLineId` | May split one line across tasks |
| `assignedTo` | email / null |
| `status` | `open`, `claimed`, `in_progress`, `done`, `cancelled` |
| `claimToken` / `version` | Concurrency |
| `qtyToPick` | |
| `qtyPicked` | |

### `wms_pick_confirmations`

Audit of scans per task.

---

## 6. Packing

### `wms_packages`

| Field | Notes |
|-------|-------|
| `packageId` | Unique printable ID |
| `orderId` | |
| `parentPackageId` | Pallet hierarchy |
| `status` | `open`, `verified`, `sealed` — **sealed** allowed per carton when contents verified (order may still be picking) |

### `wms_package_contents`

`ownerId`, `skuKey`, `qtyPacked`, **`lotId?`**, **`serialId?`**, `verifiedAt`, `verifiedBy`, `orderLineId`.

### `wms_packing_discrepancies`

Requires auth approval for partial/short pack.

---

## 7. Dispatch

### `wms_shipments`

| Field | Notes |
|-------|-------|
| `shipmentId` | |
| `carrier` | |
| `trackingNumber` | |
| `status` | `draft`, `dispatched` |
| `dispatchedAt` | |

### `wms_shipment_lines`

`ownerId`, `packageId`, `skuKey`, `qtyDispatched`, **`lotId?`**, **`serialId?`** → triggers ledger `dispatch` txn (verified qty only).

---

## 8. Counting and admin

### `wms_cycle_counts` / `wms_cycle_count_lines`

Worksheet for audits; post `adjust` transactions.

### `wms_reorder_rules`

`skuKey`, `reorderPoint`, `reorderQty`, notify list.

---

## 9. Print

### `wms_label_templates`

Owner-specific layouts (JSON; ZPL later).

### `wms_print_jobs`

`templateId`, `packageId` / `shipmentId`, `documentType` (pouch, carton, pallet, packing_list, delivery_note), `pdfArtifactUri` or buffer ref, `revision`, `status`, `requestedBy`, `reprintOf?`.

---

## 10. Relationships (ER sketch)

```text
wms_stock_owners 1──* wms_orders
wms_warehouses 1──* wms_locations
wms_warehouses 1──* wms_stock_balances
wms_orders 1──* wms_order_lines 1──* wms_pick_tasks
wms_orders 1──* wms_packages 1──* wms_package_contents
wms_shipments *──* wms_packages
wms_stock_transactions *── (references many entities)
```

---

## 11. Proposed API endpoints (illustrative)

Prefix: `/api/wms/v1` (authenticated).

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/warehouses` | List |
| POST | `/warehouses` | Create |
| GET | `/owners` | 3PL clients |
| GET | `/balances` | Filter by warehouse, owner, SKU |
| POST | `/orders` | Create outbound |
| POST | `/orders/:id/confirm` | Reserve stock |
| POST | `/pick-tasks/:id/claim` | Employee claim |
| POST | `/pick-tasks/:id/confirm` | Pick qty |
| POST | `/packages` | Create package |
| POST | `/packages/:id/verify-line` | Scan confirm |
| POST | `/shipments/:id/dispatch` | Deduct on_hand |
| POST | `/transactions/reverse` | Controlled reversal |
| GET | `/print-jobs/:id` | Label payload |

Exact shapes defined in Phase 02 OpenAPI.

---

## 12. Compatibility with existing data

| Existing collection | Strategy |
|---------------------|----------|
| `partfinder_inventory` | **Unchanged** until per-warehouse cutover; then read-only/hidden for that warehouse; opening balance import only with dry-run (D17) |
| Master data rows | SKU master via `partId` mapping |
| `partfinder_audit_logs` | Duplicate summary events from WMS service |

**No deletes** on production tenant data in migration scripts without explicit backup step.