# FLOW-06 — UX handoff to UX-01 and implementation phases

**Purpose:** Tell UX-01 (and later engineering) **what to design** so the Workbook feels like one connected system—not isolated worksheets.

**Stop:** No UI replacement in FLOW-01; prototypes remain in `docs/wms-ux/` until product-owner sign-off.

---

## 1. UX-01 must design (from FLOW)

| UX-01 artifact | FLOW inputs |
|----------------|-------------|
| `UX-01-design-audit.md` | Current nav fragmentation → hub recommendation (FLOW-03 §7) |
| `UX-02-design-system.md` | Context bar fields (FLOW-03 §1); PROTO banners (modules 8–19) |
| `UX-03-screen-blueprints.md` | All 12+ screens; extend with **receiving hub** stepper binding LIVE sections |
| `UX-04-interaction-standards.md` | Transition table (FLOW-03); idempotency modals (FLOW-04) |
| `UX-05-prototype-review.md` | Success metrics (FLOW-02 §6) |
| HTML preview | Add **task tray** placeholder on picking/packing panes |

---

## 2. Screens — controls, states, interactions

### 2.1 Global shell (all roles)

| Element | States | Interactions |
|---------|--------|--------------|
| Context bar | WH selected / missing; gate OK / closed | Change WH reloads dependents |
| Staging gate chip | supported, commit, rollback, tier | Link to Admin |
| Nav rail | LIVE vs PROTO section badges | Single click; no duplicate labels |
| Task tray (PLANNED) | Collapsed / pinned / wave id | Resume scroll position |
| Status region | Info / warning / error | Assertive for validation summary |

### 2.2 Dashboard (LIVE target)

| Control | Interaction |
|---------|-------------|
| KPI cards | Click → filtered worksheet |
| Exception list | Jump to QC / put-away / discrepancies |
| Refresh | 60s optional auto |

### 2.3 Receiving hub (LIVE — consolidate 8 nav items)

| Step | Controls | States |
|------|----------|--------|
| Expected | Grid draft, import, save | `open`, `partially_received`, `received` |
| Receive | Paste, column width, confirm modal | Draft vs posted (read-only) |
| QC | Accept/reject split, note | `pending`, `partial`, `rejected` |
| Put-away | Task list, bin combo, complete | `open`, `completed` |
| Discrepancies | Read-only flags | Over/damage |

**Required modals:** Confirm receipt (summary + idempotency); disposition (supervisor).

### 2.4 Inventory explorer (LIVE read)

| Control | Behavior |
|---------|----------|
| Balance grid | Server pagination; sticky header |
| Row lock icon | Posted ledger rows |
| Drill to ledger | Filter by skuKey + location |

### 2.5 Administration (LIVE)

| Control | Behavior |
|---------|----------|
| Record preflight | Admin only |
| Grant matrix | WH + owner checkboxes + perm flags |
| ERP outbox peek | Read-only table |

### 2.6 PROTO modules (8–15)

Design full fidelity with **amber Prototype banner**; primary actions disabled or mock; must show:

- Orders: reserve button (greyed) + explanation tooltip.  
- Picking: claim + scan fields.  
- Packing: three-pane layout.  
- Dispatch: confirm deduct warning copy (“posts ledger”).

---

## 3. State machines UX must render

| Entity | States to show in UI |
|--------|----------------------|
| Expected receipt | open · partially_received · received · closed (PLANNED) |
| Receipt line | pending QC · partial · accepted · rejected · quarantined |
| Put-away task | open · in_progress · completed |
| Order (PLANNED) | draft · reserved · picking · packing · ready · partially_shipped · shipped |
| Pick task (PLANNED) | open · claimed · in_progress · done · short |
| Carton (PLANNED) | open · verifying · sealed · dispatched |
| Outbox event | pending · sent · failed |

---

## 4. Permission-aware UI rules

| Condition | UI |
|-----------|-----|
| `!receiving` | Hide/disable confirm receipt |
| `!qc` | Disable QC apply |
| `!supervise` | Hide disposition; show “escalate” |
| Scoped owner | Owner combo entries = grant list only |
| Gate closed | Mutation buttons disabled + banner |

---

## 5. UX success criteria (from FLOW-02 §6 + FLOW-01)

| Metric | Design target |
|--------|---------------|
| Expected → first receipt (partial) | ≤ 12 steps |
| QC per line | ≤ 5 interactions |
| Put-away per line | ≤ 4 interactions |
| Pick per line (PROTO) | ≤ 4 scans |
| Pack pouch (PROTO) | ≤ 3 scans |
| Error visibility | Inline + summary bar; no banner-only |
| Large grids | Virtualization + &lt; 400 ms first paint (goal) |
| Navigation | Receiving hub ≤ 1 click between steps |

**Non-negotiable:** Keep confirm modals for stock posts even if step count rises.

---

## 6. Implementation phasing (after UX sign-off)

| Phase | UX deliverable consumed |
|-------|-------------------------|
| UX-01.1 | Shell + tokens + context bar |
| UX-01.2 | Receiving hub layout (reuse ViewModels) |
| UX-01.3 | Shared grid host |
| 03B | Order + pick flows per FLOW-05 #3 |
| 04 | Pack/label per FLOW-05 #4 |
| 05 | Dispatch per FLOW-05 #5 |

---

## 7. Document index (FLOW package)

| File | Content |
|------|---------|
| [FLOW-01-end-to-end-workflow.md](./FLOW-01-end-to-end-workflow.md) | 19 modules, ledger rules |
| [FLOW-02-user-roles-and-journeys.md](./FLOW-02-user-roles-and-journeys.md) | Roles, journeys, budgets |
| [FLOW-03-navigation-and-screen-transitions.md](./FLOW-03-navigation-and-screen-transitions.md) | Transition spec |
| [FLOW-04-exceptions-and-approvals.md](./FLOW-04-exceptions-and-approvals.md) | Handoffs, approvals |
| [FLOW-05-visual-flowcharts.md](./FLOW-05-visual-flowcharts.md) | Mermaid diagrams |
| **FLOW-06** (this file) | UX handoff |

---

## 8. Product-owner review checklist

- [ ] Ledger rule accepted: reserve/pick/pack do not deduct on-hand  
- [ ] Receiving hub IA approved over fragmented nav  
- [ ] nineteen modules mapped to phase gates  
- [ ] Approval matrix (FLOW-04) accepted  
- [ ] Role landing screens (FLOW-02) accepted  
- [ ] UX-01 may proceed to visual rollout planning  

---

*Related UX package:* `docs/wms-ux/UX-01` … `UX-05` + HTML preview.