# FLOW-04 — Exceptions, approvals, and handoffs

**Rule:** Physical stock accuracy overrides click reduction. Supervisory approval is explicit, audited, and cannot be bypassed by operators without `supervise` permission.

---

## 1. Handoff matrix

| From | To | Trigger | System handoff |
|------|-----|---------|----------------|
| Planner / ERP | Receiving | Expected ASN created | Expected receipt doc |
| Receiver | QC | Receipt confirmed | Lines `pending` QC + blocked qty |
| QC | Put-away | Accept partial/full | Put-away tasks |
| QC | Supervisor | Reject / quarantine | Disposition queue |
| Supervisor | ERP | Write-off confirmed | Outbox event + ledger |
| Put-away | Inventory | Task complete | Balances updated (move) |
| Planner (PLANNED) | Picker | Wave released | Pick tasks |
| Picker | Packer (PLANNED) | Staging complete | Staging balance by order |
| Packer | Dispatch (PLANNED) | Carton sealed | Shipment ready flag |
| Dispatch | ERP (PLANNED) | Confirm dispatch | Outbox + deduct |

---

## 2. Approval requirements

| Scenario | Approver | LIVE / PLANNED |
|----------|----------|----------------|
| Partial receipt (under expected) | None (operational) | LIVE |
| Over-delivery | Receiver flags; manager review | Partial UI |
| QC reject physical disposition | Supervisor (`supervise`) | LIVE |
| Write-off qty | Supervisor + reason | LIVE |
| Stock adjustment (cycle count) | Manager + note | PLANNED |
| Reservation override / short ship | Manager | PLANNED |
| Substitution SKU on order | Manager | PLANNED |
| Partial dispatch | Manager + reason | PLANNED |
| Packing content override | Supervisor scan exception | PLANNED |
| ERP sync failure credit | Manager reconciliation | PLANNED |
| Reversal of posted receipt | **Forbidden** silent; adjustment only | Design |

---

## 3. Exception paths (detail)

### 3.1 Partial receipt

- **Normal:** Expected remains `partially_received`; remaining qty on line.
- **Cancel remainder:** Manager closes expected (PLANNED formal close).
- **Retry:** Additional confirm with **new** idempotency key.

### 3.2 QC rejection

- **Normal:** Qty stays on-hand at receiving location; `blockedQty` increased.
- **No auto write-off** (03A.1).
- **Recovery:** Supervisor disposition → `writeOff` or return-to-supplier workflow (PLANNED docs).

### 3.3 Quarantine

- Stock blocked; no put-away task until released by supervisor QC decision.

### 3.4 Put-away failure

- Wrong bin scanned → validation error inline; task stays `open`.
- **Retry:** Complete with corrected location.

### 3.5 Order shortage (PLANNED)

- Reserve partial → remainder backorder flag.
- Pick short → manager approves short ship or substitute line.

### 3.6 Pick duplicate (PLANNED)

- Second confirm on same pick line → idempotent success or 409 with no double move.

### 3.7 Packing change (PLANNED)

- Remove pouch before seal → supervisor unlock carton.
- After seal → void requires manager + audit (no silent unpack).

### 3.8 Partial dispatch (PLANNED)

- Some cartons ship; others hold → dispatch txn partial qty; order `partially_shipped`.

### 3.9 ERP failure

- **LIVE:** Outbox pending; receiving events stored.
- **Recovery:** Retry with backoff; manager marks reconciled when external system matches.

### 3.10 Cancellation

| Object | Allowed | Effect |
|--------|---------|--------|
| Draft expected line | Yes (before post) | Delete line |
| Posted receipt | No | Adjustment only |
| Open pick task | PLANNED | Release claim |
| Sealed carton | PLANNED | Supervisor void |

---

## 4. Authorization errors (uniform UX)

| HTTP | Meaning | UI |
|------|---------|-----|
| 401 | Not signed in | Login |
| 403 | Scope / grant / gate | Explain missing grant or gate |
| 404 | Wrong id | Refresh worksheet |
| 409 | Conflict / duplicate claim | Refresh + toast |
| 422 | Validation | Inline field errors |

---

## 5. 3PL isolation exceptions

| Case | Behaviour |
|------|-----------|
| Operator A sees owner B in API | **Must not** — filtered lists |
| Operator attempts owner B receipt | 403 at create |
| Admin views all owners | Allowed |
| Stock owner portal (PLANNED) | JWT role `owner` → single ownerId |

---

## 6. Audit expectations

| Event | Store |
|-------|-------|
| Receipt confirm | `wms_goods_receipts` + ledger |
| QC decision | `wms_qc_inspections` + audit log |
| Disposition | Disposition record + writeOff txn |
| Grant change | `wms_operator_grants` |
| Pick/pack/dispatch (PLANNED) | Task + package revision history |

---

*Diagrams:* [FLOW-05-visual-flowcharts.md](./FLOW-05-visual-flowcharts.md)  
*UX:* [FLOW-06-ux-handoff.md](./FLOW-06-ux-handoff.md)