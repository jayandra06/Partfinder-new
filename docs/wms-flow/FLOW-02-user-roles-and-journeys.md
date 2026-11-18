# FLOW-02 — User roles and journeys

**Principle:** Employees see **only** warehouses, stock owners, and operations permitted by `wms_operator_grants` and session profile (`GET /wms/v1/session/profile`). Admins see all masters but mutations still respect audit.

---

## 1. Role definitions

| Role | System identity | Typical grants |
|------|-----------------|----------------|
| Warehouse administrator | Org/WMS admin | All warehouses & owners; setup CRUD; grants; preflight |
| Warehouse manager | Admin or supervise grant | All or multi-site; approvals; dashboards |
| Receiving employee | Operator + receiving | Scoped WH + owner; expected + confirm receipt |
| QC inspector | Operator + qc | Same scope; QC worksheets |
| Put-away operator | Operator + putaway | Same scope; put-away tasks |
| Picker | Operator (PLANNED) | Pick tasks; staging moves |
| Packer | Operator (PLANNED) | Pack station; label preview |
| Dispatch employee | Operator (PLANNED) | Shipment confirm |
| Stock owner (3PL client) | Portal / read (PLANNED) | Own owner dimension only |
| 3PL supervisor | Cross-owner manager (PLANNED) | Multiple owners; billing reports |

---

## 2. Landing screens (target IA post UX-01)

| Role | Landing | Why |
|------|---------|-----|
| Administrator | **Dashboard** + context bar | Health, gate, open tasks |
| Manager | **Dashboard** | KPIs + exceptions queue |
| Receiver | **Receiving hub** (step Expected) | Minimize nav hops |
| QC | **Receiving hub** step QC | Or QC queue filtered “pending” |
| Put-away | **Put-away tasks** (My tasks) | Resume open work |
| Picker (PLANNED) | **Task tray** + active wave | Claim next task |
| Packer (PLANNED) | **Packing station** | Scan-first layout |
| Dispatch (PLANNED) | **Dispatch queue** | Ready-to-ship list |
| Stock owner (PLANNED) | **Inventory explorer** (read) | ATP by agreement |
| 3PL supervisor (PLANNED) | **Reporting** multi-owner | |

**Current WinUI:** Single Workbook with left nav sections; **no** role-based landing yet (FLOW design target).

---

## 3. Journey maps (daily work)

### 3.1 Warehouse administrator

| Task | Screens | Interactions (target) | LIVE? |
|------|---------|----------------------|-------|
| Provision new 3PL owner | Stock owners → create | 4–6 clicks | Yes |
| Onboard operator | Administration → grant scope + perms | 8–10 clicks | Yes |
| Record staging preflight | Administration | 2 clicks + confirm | Yes |
| Review ledger dispute | Balances → ledger drill | 5+ search | Read |

### 3.2 Receiving employee

| Task | Screens | Steps (target) | LIVE? |
|------|---------|----------------|-------|
| Register expected ASN | Expected → add lines → save | ≤ 8 | Yes |
| Receive partial shipment | Receive → paste grid → confirm modal | ≤ 6 after draft | Yes |
| Handle over-delivery | Discrepancies worksheet | Review only | Partial list |

### 3.3 QC inspector

| Task | Screens | Steps | LIVE? |
|------|---------|-------|-------|
| Partial QC | QC row → accept/reject qty → apply | 4 per line | Yes |
| Escalate reject | Disposition (supervisor) | + modal | Yes (supervise) |

### 3.4 Put-away operator

| Task | Screens | Steps | LIVE? |
|------|---------|-------|-------|
| Complete put-away | Put-away → select task → bin → complete | 4 | Yes |

### 3.5 Picker (PLANNED — design)

| Task | Screens | Steps (target) |
|------|---------|----------------|
| Claim wave task | Task tray → claim | 2 |
| Pick line | Scan loc → scan SKU → confirm qty | 3–4 per line |
| Report short | Short reason → manager queue | +3 |

No on-hand deduct — only `move` to staging.

### 3.6 Packer / dispatch (PLANNED)

| Task | Screens | Steps (target) |
|------|---------|----------------|
| Verify pouch | Scan pouch → match customer PN | 2 per pouch |
| Seal carton | Contents grid all green → seal | 3 |
| Confirm dispatch | Dispatch queue → verify cartons → confirm | 5 + approval if partial |

Dispatch posts **deduct**.

---

## 4. Permissions and visibility

| Data | Admin | Scoped operator |
|------|-------|-----------------|
| Warehouse dropdown | All active | Grant list only (`listWarehouses` filter) |
| Stock owner dropdown | All active | Grant list only |
| Balances / ledger | All (read) | Filtered by owner/WH in API |
| Expected / receipt create | Yes if receiving perm | Owner must be in grant |
| QC disposition / write-off | Supervise or admin | Operators without supervise: QC only |
| PROTO modules | Banner + read-only mock | Hidden or banner |

---

## 5. Success and failure UX (per role)

| State | User sees | Next action |
|-------|-----------|-------------|
| Success post | Toast + link to balances | Continue next line or task |
| 403 scope | Grant missing message | Contact admin |
| Gate closed | Banner + disabled confirm | Admin preflight |
| Idempotent duplicate | “Already recorded” tone | Refresh list |
| 409 concurrency | Refresh prompt | Reload worksheet |
| QC reject pending | Blocked qty in balances | Supervisor disposition |
| Pick conflict (PLANNED) | “Claimed by other user” | Claim different task |

---

## 6. Interaction budgets (design targets — UX-01 §6)

| Workflow | Target steps (trained) | Must not shortcut |
|----------|------------------------|-------------------|
| Expected → first receipt (60/100) | ≤ 12 | Idempotency confirm modal |
| QC one line partial | ≤ 5 | Qty validation |
| Put-away one line | ≤ 4 | Bin confirmation |
| Pick one line (PLANNED) | ≤ 4 scans | Location verify |
| Pack one pouch (PLANNED) | ≤ 3 scans | Customer PN match |
| Dispatch order (PLANNED) | ≤ 6 | Carton verify + partial approval |

---

## 7. Employee resume after interrupt

| Interrupted at | Resume mechanism |
|----------------|------------------|
| Draft receipt grid | Draft kept until navigate away (future: local draft save) |
| Mid expected entry | Same worksheet reload |
| Open put-away task | Put-away list retains task id |
| Active pick (PLANNED) | Task tray pins wave; scroll position restored |
| Pack session (PLANNED) | Carton id in URL/context bar |

---

*Next:* [FLOW-03-navigation-and-screen-transitions.md](./FLOW-03-navigation-and-screen-transitions.md)