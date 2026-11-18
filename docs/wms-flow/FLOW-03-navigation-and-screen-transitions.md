# FLOW-03 — Navigation and screen transitions

**Format:** `Current state → Action → Validation → Server transaction → New state → Notification → Next screen`

Applies to **target** workbook IA (receiving hub + context bar). Current ListView sections map 1:1 where noted.

---

## 1. Global context bar (all operational screens)

| Field | Source |
|-------|--------|
| Warehouse | `SelectedWarehouse` / session default |
| Stock owner filter | Scoped combos |
| Operator email | JWT profile |
| Staging gate | `GET staging-gate/latest` |
| Density | User preference (UX-01) |

Changing warehouse **reloads** dependent worksheets (balances, tasks).

---

## 2. LIVE — Receiving → QC → Put-away

### 2.1 Create expected receipt

| Step | Detail |
|------|--------|
| State | No expected doc |
| Action | User: New expected + owner + WH + lines |
| Validation | DTO; owner/warehouse scope; limits |
| Transaction | Insert `wms_expected_receipts` (no stock) |
| New state | Expected `open` |
| Notification | Status message success |
| Next screen | Stay on Expected; or hub step 2 |

### 2.2 Confirm goods receipt (partial)

| Step | Detail |
|------|--------|
| State | Expected `open` / `partially_received` |
| Action | Confirm receipt + idempotency key |
| Validation | Gate open; permissions; qty ≤ remaining optional rules |
| Transaction | Mongo txn: receipt docs + `receive` + `qcBlock` per line |
| New state | Goods receipt posted; lines `pending` QC |
| Notification | Toast; show idempotency key |
| Next screen | QC step or receipt lines |

**Duplicate:** Same key → no second post; notify duplicate.

### 2.3 QC partial accept/reject

| Step | Detail |
|------|--------|
| State | Line `pending` QC |
| Action | Apply `qtyAccepted` / `qtyRejected` |
| Validation | Sum ≤ received; QC perm |
| Transaction | `qcRelease` on accepted; rejected stays blocked |
| New state | Put-away tasks for accepted qty |
| Notification | Line status updated |
| Next screen | Put-away list |

### 2.4 Supervisor disposition (reject write-off)

| Step | Detail |
|------|--------|
| State | Rejected blocked qty |
| Action | Disposition type + reason + qty |
| Validation | Supervise perm; approval note |
| Transaction | `writeOff` when confirmed |
| New state | Blocked reduced; audit entry |
| Notification | Explicit “written off” message |
| Next screen | QC or balances |

### 2.5 Complete put-away

| Step | Detail |
|------|--------|
| State | Put-away task `open` |
| Action | Complete + toLocationId + idempotency |
| Validation | Put-away perm; location scoped |
| Transaction | `move` recv/staging → storage |
| New state | Task `completed` |
| Notification | Success |
| Next screen | Next task or balances |

---

## 3. PLANNED — Order → reservation → pick

| Step | Transition |
|------|------------|
| Import order | File/API → validate owner → insert order lines (idempotent ext id) |
| Reserve | Validate ATP → txn: `reserve` only (**no on-hand deduct**) |
| Release reserve | Cancel line / short approval → `releaseReservation` |
| Create pick wave | Manager action → pick tasks generated |
| **Claim task** | Optimistic lock / compare-and-set on task.assignee |
| Confirm pick | Scan validation → `move` storage → staging |
| Short pick | Task paused → manager substitute approval |

**Concurrent picks:** Two operators cannot claim same task; 409 → toast + refresh task list.

---

## 4. PLANNED — Pick → pack → label

| Step | Transition |
|------|------------|
| Start pack session | Scan order or carton id |
| Add pouch | Scan customer PN → verify qty against order line |
| Mismatch | Block seal; supervisor override |
| Seal carton | All pouches verified → carton status `sealed` |
| Print label | Generate PDF preview; print job id stored (no fake post) |
| Reprint | Requires reason code (audit) |

**No stock deduct** at pack time.

---

## 5. PLANNED — Pack → dispatch → ERP

| Step | Transition |
|------|------------|
| Shipment ready | All cartons sealed or partial dispatch approved |
| Dispatch confirm | Modal summary → txn: `dispatchConfirm` + release reserve |
| ERP outbox | Event enqueued same txn |
| ERP ack fail | Outbox `pending` → retry worker |
| Reconciliation | Manager matches ERP vs ledger |

---

## 6. PLANNED — Returns & stock count

| Flow | Ledger |
|------|--------|
| RMA receive | `receive` to returns location + QC path |
| Count variance | Adjustment proposal → approval → `adjustment` txn |
| Reverse dispatch | Rare; credit note + `receive` with approval |

---

## 7. Navigation map (current → target)

| Current section id | Target group |
|--------------------|--------------|
| `dashboard` | Overview |
| `warehouse_master`, `locations`, `stock_owners` | Setup |
| `sku_registry`, `sku_identifiers`, `uom` | Setup |
| `stock_balances`, `movement_ledger` | Inventory (read) |
| `expected_receipts` … `receiving_discrepancies` | **Receiving hub** (single lane) |
| `orders`, `picking` … `dispatch` | Operations **PROTO** |
| `administration` | Admin |

---

## 8. Search, filter, Excel (all roles)

| Worksheet | Search | Filter | Excel |
|-----------|--------|--------|-------|
| Expected / receipts | Workbook search box (client) | Owner, WH | Export LIVE |
| Balances | SKU / location | Server pagination | Export |
| Ledger | — | WH, owner, sku | Export (PLANNED full) |
| Orders (PLANNED) | Order id, customer PN | Wave, status | Import template |

---

## 9. Simultaneous activity & duplicate prevention

| Activity | Mechanism |
|----------|-----------|
| Two receivers same receipt | Idempotency key on confirm |
| Two QC on same line | Optimistic version / last-write wins with 409 |
| Two pickers same task | Atomic claim field |
| Same SKU multi-bin pick | Task line locked to one assignee |
| Pack + dispatch same carton | Carton state machine: `sealed` before `dispatched` |

---

*Next:* [FLOW-04-exceptions-and-approvals.md](./FLOW-04-exceptions-and-approvals.md), [FLOW-05-visual-flowcharts.md](./FLOW-05-visual-flowcharts.md)