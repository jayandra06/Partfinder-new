# UX-03 — Screen Blueprints (Warehouse Workbook)

Legend: **LIVE** = implemented API-backed · **PROTO** = design prototype only (no live posting).

---

## 1. Warehouse Dashboard — **LIVE** (enhance)

| | |
|--|--|
| **Primary role** | Supervisor, administrator |
| **Main task** | Monitor health, staging gate, open operational tasks |
| **Key columns / widgets** | Gate status, txn probe, open QC count, open put-away, receipt exceptions, ERP outbox pending |
| **Actions** | Refresh, jump to task, run preflight (admin) |
| **Behavior** | KPI cards clickable → filtered worksheet; auto-refresh every 60s optional |

---

## 2. Warehouse Master & Locations — **LIVE**

| | |
|--|--|
| **Role** | Warehouse administrator |
| **Task** | Maintain sites, zones, bins |
| **Columns** | Code, name, timezone, active; zone; location path, type, pickable |
| **Actions** | Add, deactivate, bulk import paths (CSV) |
| **Behavior** | Master-detail: warehouse list left, locations grid right; validation inline |

---

## 3. SKU Registry & Inventory Explorer — **LIVE** / read-only balances

| | |
|--|--|
| **Role** | Master data steward, inventory analyst |
| **Task** | Register SKUs, map identifiers, explore on-hand |
| **Columns (SKU)** | SkuId, name, UOM, status, identifiers |
| **Columns (balances)** | Owner, warehouse, location, on-hand, blocked, reserved, ATP |
| **Actions** | Create SKU, add identifier, export, drill to ledger |
| **Behavior** | Split view: registry top, explorer bottom with server pagination; **posted rows locked** |

---

## 4. Goods Receiving — **LIVE**

| | |
|--|--|
| **Role** | Receiving operator |
| **Task** | Expected ASN → physical receipt |
| **Columns** | Expected: PO, supplier, line SKU, qty expected, received, remaining; Receipt: SKU, qty, lot, serial, flags |
| **Actions** | Save expected, import lines, confirm receipt (idempotent), paste lines |
| **Behavior** | **Stepper** step 1–2; idempotency key shown after confirm; duplicate banner |

---

## 5. Quality Control — **LIVE**

| | |
|--|--|
| **Role** | QC operator |
| **Task** | Accept / reject / quarantine quantities |
| **Columns** | Line ID, SKU, qty received, accept, reject, status, decided by |
| **Actions** | Apply QC partial, supervisor disposition |
| **Behavior** | Reject keeps stock blocked; disposition requires reason + confirm modal |

---

## 6. Put-away — **LIVE**

| | |
|--|--|
| **Role** | Put-away operator |
| **Task** | Move accepted qty to storage |
| **Columns** | Task ID, SKU, qty, from loc, to loc, status, assignee |
| **Actions** | Complete, scan bin barcode (future) |
| **Behavior** | “My tasks” default filter; keyboard: Enter complete with suggested bin |

---

## 7. Orders & Reservation Management — **PROTO** (Phase 03B)

| | |
|--|--|
| **Role** | Planner |
| **Task** | View demand, reserve stock |
| **Columns** | Order ID, owner, lines, reserved, short |
| **Actions** | Reserve, release (disabled in proto) |
| **Behavior** | `WmsPrototypeBanner`; greyed primary actions |

---

## 8. Multi-Employee Picking — **PROTO** (Phase 03B)

| | |
|--|--|
| **Role** | Pick operator |
| **Task** | Pick to staging by wave |
| **Columns** | Pick list, location, SKU, qty, picked, conflict indicator |
| **Actions** | Confirm pick, report short |
| **Behavior** | Real-time assignment mock; barcode field prominent |

---

## 9. Packing Station & Pouch Verification — **PROTO** (Phase 04)

| | |
|--|--|
| **Role** | Packer |
| **Task** | Verify pouch/carton contents |
| **Columns** | Order, pouch ID, customer PN, qty, scan result |
| **Actions** | Print label preview, seal carton |
| **Behavior** | Split UI: scan pane + contents grid + label preview pane |

---

## 10. Dispatch & Shipping — **PROTO** (Phase 05)

| | |
|--|--|
| **Role** | Dispatch clerk |
| **Task** | Confirm shipment, carrier ref |
| **Columns** | Shipment, carrier, cartons, weight, status |
| **Actions** | Post dispatch (proto) |

---

## 11. Reporting & Analytics — **PROTO** (Phase 06)

| | |
|--|--|
| **Role** | Supervisor |
| **Task** | Throughput, accuracy, dwell time |
| **Widgets** | Receipts/day, pick rate, QC reject % |
| **Behavior** | Read-only charts; export CSV |

---

## 12. Administration & ERP Integration — **LIVE** (partial)

| | |
|--|--|
| **Role** | WMS administrator |
| **Task** | Grants, staging evidence, ERP outbox peek |
| **Columns** | Operator email, warehouses, owners, permissions |
| **Actions** | Upsert grant, view outbox (read-only) |
| **Behavior** | Matrix picker for warehouse × owner scope |

---

## Navigation IA (proposed)

```
WMS Sidebar
├── Dashboard
├── Setup (Master, Locations, SKU, UOM)
├── Inventory (Balances, Ledger) [read-only]
├── Operations
│   ├── Receiving hub (Expected · Receive · Lines)
│   ├── QC
│   └── Put-away
├── Future [PROTO]
│   ├── Orders & Reservations
│   ├── Picking
│   ├── Packing
│   └── Dispatch
├── Reports [PROTO]
└── Administration
```

---

## Role × screen matrix

| Screen | Receiver | QC | Put-away | Supervisor | Admin |
|--------|:--------:|:--:|:--------:|:----------:|:-----:|
| Dashboard | ○ | ○ | ○ | ● | ● |
| Receiving | ● | ○ | ○ | ○ | ○ |
| QC | ○ | ● | ○ | ● | ○ |
| Put-away | ○ | ○ | ● | ○ | ○ |
| Balances | ○ | ○ | ○ | ● | ● |
| Admin | — | — | — | ○ | ● |
| PROTO modules | — | — | — | preview | preview |

---

*Implementation order after PO approval: Dashboard + Context bar → Shared grid → Receiving hub.*