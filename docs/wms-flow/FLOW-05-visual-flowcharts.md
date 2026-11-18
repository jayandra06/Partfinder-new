# FLOW-05 — Visual flowcharts

Reviewable Mermaid diagrams. **Green paths** = LIVE (03A); **dashed** = PLANNED.

---

## 1. Overall Warehouse Workbook

```mermaid
flowchart TB
  subgraph shell [WMS Workbook shell]
    CTX[Context bar: WH · Owner · Operator · Gate]
    NAV[Navy navigation rail]
  end

  NAV --> DASH[Dashboard]
  NAV --> SETUP[Setup: WH · Locations · Owners · SKU]
  NAV --> INV[Inventory: Balances · Ledger RO]
  NAV --> INBOUND[Receiving hub]
  NAV --> OUT[Outbound PROTO]
  NAV --> RPT[Reports PROTO]
  NAV --> ADMIN[Administration]

  INBOUND --> E[Expected]
  INBOUND --> R[Receive]
  INBOUND --> QC[QC]
  INBOUND --> PA[Put-away]

  OUT --> ORD[Orders]
  OUT --> PICK[Picking]
  OUT --> PACK[Packing]
  OUT --> DSP[Dispatch]

  CTX --> NAV
```

---

## 2. Receiving → QC → Put-away (LIVE)

```mermaid
flowchart LR
  A[Expected open] -->|Add lines| B[Expected ready]
  B -->|Confirm receipt idempotent| C{Gate + scope OK?}
  C -->|No| X403[403 / gate banner]
  C -->|Yes| D[Txn: receive + qcBlock]
  D --> E[Lines pending QC]
  E -->|Partial QC| F[qcRelease accepted]
  E -->|Reject qty| G[Blocked on-hand]
  G -->|Supervisor disposition| H[writeOff optional]
  F --> I[Put-away task open]
  I -->|Complete move| J[Storage bin ATP]
```

---

## 3. Order → Reservation → Picking (PLANNED)

```mermaid
flowchart TB
  O[Order imported] --> V{Owner + ATP OK?}
  V -->|No| SHORT[Backorder / short flag]
  V -->|Yes| RES[Txn: reserve only]
  RES --> NOTE1[On-hand unchanged]
  RES --> W[Release wave]
  W --> T[Pick tasks created]
  T --> CL{Claim task atomic}
  CL -->|Taken| T2[Other employee]
  CL -->|OK| PK[Scan pick confirm]
  PK --> MV[Txn: move to staging]
  MV --> NOTE2[On-hand total conserved]
```

---

## 4. Picking → Packing → Label printing (PLANNED)

```mermaid
flowchart LR
  S[Stock in staging loc] --> PS[Pack session start]
  PS --> SC[Scan pouch / customer PN]
  SC --> V{Match order line?}
  V -->|No| ERR[Block + supervisor]
  V -->|Yes| AD[Add to carton grid]
  AD --> SE{All lines verified?}
  SE -->|No| SC
  SE -->|Yes| SL[Seal carton]
  SL --> LP[Label PDF preview / print]
  LP --> NOTE[No stock deduct]
```

---

## 5. Packing → Dispatch → ERP confirmation (PLANNED)

```mermaid
flowchart TB
  C[Cartons sealed] --> RD{Shipment ready?}
  RD -->|Partial| MGR[Manager partial approval]
  MGR --> DC
  RD -->|Full| DC[Dispatch confirm modal]
  DC --> TXN[Txn: dispatchConfirm + release reserve]
  TXN --> DED[On-hand deduct]
  TXN --> OB[ERP outbox event]
  OB --> ACK{ERP ack?}
  ACK -->|Fail| RET[Retry queue]
  ACK -->|OK| DONE[Order shipped state]
```

---

## 6. Returns and reconciliation (PLANNED)

```mermaid
flowchart LR
  RMA[RMA authorized] --> REC[Receive to returns loc]
  REC --> QC2[QC path]
  QC2 --> ST[Restock or write-off]
  CNT[Cycle count] --> VAR{Variance?}
  VAR -->|Yes| APP[Manager approval]
  APP --> ADJ[Adjustment txn]
  ERP2[ERP mismatch] --> RECON[Reconciliation worksheet]
```

---

## 7. 3PL client isolation and permissions

```mermaid
flowchart TB
  JWT[JWT + org] --> G[WmsSessionGuard]
  G --> ENT{wmsEnabled?}
  ENT -->|No| F403[403]
  ENT -->|Yes| GR[Load operator grants]
  GR --> ADM{WMS admin?}
  ADM -->|Yes| ALL[allowedWarehouseIds = all]
  ADM -->|No| SCP[Scoped WH + owner lists]
  SCP --> API[list/create APIs filter scope]
  API --> UI[WinUI combos show subset only]
```

---

## 8. Employee onboarding and daily task flows

```mermaid
flowchart TB
  subgraph onboard [Onboarding]
    O1[Org entitled] --> O2[Admin creates WH + owners]
    O2 --> O3[Grant operator scope + perms]
    O3 --> O4[Operator first login]
    O4 --> O5[Session profile loaded]
  end

  subgraph daily [Daily task]
    D1[Land on role home] --> D2{Task queue empty?}
    D2 -->|No| D3[Resume pinned task tray]
    D2 -->|Yes| D4[Pull next / search]
    D3 --> D5[Complete with scan/keyboard]
    D5 --> D6[Success toast + next suggested screen]
  end

  O5 --> daily
```

---

## Diagram notes for product review

| # | Key decision |
|---|----------------|
| 3 | Reservation never deducts on-hand. |
| 4 | Packing verifies; does not deduct. |
| 5 | **Only** dispatch confirms deduct. |
| 7 | Matches `WmsSessionGuard` + list filters (LIVE). |

---

*Roles:* [FLOW-02-user-roles-and-journeys.md](./FLOW-02-user-roles-and-journeys.md)  
*UX handoff:* [FLOW-06-ux-handoff.md](./FLOW-06-ux-handoff.md)