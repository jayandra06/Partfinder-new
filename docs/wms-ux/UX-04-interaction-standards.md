# UX-04 — Interaction Standards

**Applies to:** Warehouse Workbook (future implementation phases).  
**Does not change:** Stock posting rules, API contracts, or retail Master Data behavior without separate approval.

---

## 1. Enterprise worksheet grid (target)

### 1.1 Compatibility strategy (mandatory)

| Option | Pros | Cons | Recommendation |
|--------|------|------|----------------|
| **Extract Master Data spreadsheet host** | Proven virtual scroll, filters, PartFinder-owned | Coupling to template model | **Preferred Phase 1** — adapt host for typed WMS row VMs |
| **ItemsRepeater + scroll** | Native, lightweight | Manual column resize/sort | Phase 2 for simple lists |
| **CommunityToolkit WinUI DataGrid** | Familiar API | Limited WinUI maturity | Evaluate in spike; do not adopt without perf sign-off |
| **Syncfusion / Telerik** | Full Excel UX | License cost, vendor lock | **Only** if internal spike fails performance targets |

**Rule:** Do not remove Master Data grid. Do not replace WMS ListViews until `WmsDataGrid` spike passes UX-04 §8 metrics.

### 1.2 Required behaviors

| Feature | Draft worksheets | Posted / ledger |
|---------|------------------|-----------------|
| Virtualization | Yes (>200 rows) | Yes |
| Pagination | Server-side when API paginates | Required |
| Column resize | Yes | Yes |
| Column reorder | Yes (persist per user) | Optional |
| Sort | Client + server where supported | Server preferred |
| Filter | Column + global search | Global + server query |
| Saved views | Yes (name + filter JSON local) | Read-only views |
| Multi-select | Bulk actions (export) | Export only |
| Clipboard paste | Draft lines only | **Blocked** |
| Excel import | Validated server-side | N/A |
| Excel export | Authorized columns | Full export audit log |
| Keyboard nav | Arrow, Tab, Enter, Esc | Nav only |
| Sticky header | Yes | Yes |
| Row status | Chips + icon | Lock icon on posted txn rows |
| Context menu | Edit row, delete draft | Copy ID, view ledger |

### 1.3 Validation pattern

- Inline cell error (red border + caption); summary bar lists count of errors.
- Destructive stock actions: **modal** with typed confirmation (`CONFIRM` or qty re-entry).

---

## 2. Warehouse operations UX

### 2.1 Receiving happy path (target ≤ 12 clicks)

1. Context bar: warehouse + owner pre-set from last session.
2. Receiving hub **Step 1** Expected: import or add lines → Save.
3. **Step 2** Physical: paste grid → Confirm (modal shows idempotency).
4. **Step 3** QC: inline accept/reject split → Apply.
5. **Step 4** Put-away: scan/enter bin → Complete.
6. Toast + link “View balances”.

**Safety:** Confirm receipt always shows summary modal (qty, owner, location). Cannot bypass.

### 2.2 Barcode / keyboard

| Shortcut | Action |
|----------|--------|
| `Ctrl+S` | Save draft |
| `Ctrl+Enter` | Apply primary action (context) |
| `Ctrl+Shift+V` | Paste lines |
| `F3` | Focus search |
| `Esc` | Cancel edit / close panel |
| Scan (HID) | Inserts into focused cell / search |

Future handheld: same APIs; responsive layout spec in Phase UX-02.1.

### 2.3 Picking / packing (PROTO)

- **Task tray** pinned right: active wave, progress, switch without losing scroll position.
- Packing: **three-pane** — scan | contents grid | label preview (ZPL/PDF mock).

### 2.4 Multi-employee concurrency

- Optimistic UI with **409** recovery toast (“Record updated elsewhere — refresh”).
- Row-level lock indicator when another operator holds task (future API).

---

## 3. Accessibility

| Requirement | Standard |
|-------------|----------|
| Keyboard-only | All primary flows completable without mouse |
| Focus | 2px teal ring (`WmsFocusRing`); visible on ListView items |
| Screen reader | `AutomationProperties.Name` on every grid header and action |
| Contrast | WCAG 2.1 AA for text; AAA for critical warnings |
| Scaling | Support 100–200% WinUI scale |
| Errors | `LiveSetting=Assertive` on validation summary |
| Tables | Announce sort column and filter state changes |

---

## 4. Internationalization

| Area | Approach |
|------|----------|
| Strings | RESW files per module (`Resources/Wms/`) |
| Dates | User locale via `Windows.Globalization` |
| Quantities | `NumberFormatter` with org UOM suffix |
| RTL | Defer; structure XAML for future `FlowDirection` |
| First ship | English (`en-GB` default per `BritishEnglish` helper) |

---

## 5. Performance acceptance targets (design goals)

| Metric | Goal | Measured in UX-01 app |
|--------|------|------------------------|
| Grid first paint (500 rows client) | < 400 ms | **Not measured** |
| Scroll FPS | ≥ 55 fps | **Not measured** |
| Server search debounce | 300 ms | Partial (inventory search) |
| Worksheet switch | < 150 ms | **Not measured** |
| Export 10k rows | < 8 s | **Not measured** |

Record actuals in `UX-05-prototype-review.md` after implementation spikes.

---

## 6. Error recovery

| Scenario | UX |
|----------|-----|
| API 403 | Explain missing grant; link Administration |
| Staging gate closed | Banner + read-only mode on mutations |
| Idempotent duplicate | Success tone “Already recorded” — no second post |
| Network loss | Retry banner; drafts kept in memory until navigate away (future: local draft save) |

---

## 7. Training & consistency

- **One worksheet chrome** across WMS (title, filters, grid, footer).
- **Terminology glossary** in-app (Help → WMS glossary): ATP, blocked, posted.
- PROTO screens: persistent amber **Prototype** banner — never identical to LIVE chrome.

---

## 8. Multi-user acceptance (design goals)

| Scenario | Goal |
|----------|------|
| Two receivers same SKU | No double post (server idempotency) + UI duplicate message |
| QC + put-away same line | Task state machine visible |
| Supervisor disposition | Separate permission; audited reason field |

---

*Engineering spikes required before grid replacement — see UX-05.*