# UX-01 — Design Audit (PartFinder WinUI 3)

**Phase:** Product design foundation only — **no production UI changes** in UX-01.  
**Scope audited:** Shell, Master Data spreadsheet, Warehouse Workbook, legacy Inventory, shared styles.  
**Method:** Static review of `PartFinder/PartFinder` XAML/C# (November 2026).

---

## 1. Application shell and navigation

| Area | Current state | UX issues |
|------|----------------|-----------|
| `ShellLayout.xaml` | Glassmorphism header, horizontal top nav (`ItemsRepeater`), theme-aware ambient visuals | Warehouse Workbook is a **separate top-level destination**, not a cohesive “WMS mode” with persistent context (warehouse, owner, role) |
| `ShellViewModel.cs` | “Warehouse Management” group; `Warehouse Workbook` entry when `CanAccessWmsWorkbook` | Two labels (“Warehouse Management” vs “Workbook”) confuse IA; no badge for staging/gate or task queue |
| Theme | `ThemeService` + `Colors.xaml` light/dark | Strong **blue** accent (`#1F7AE0` / `#5EA2FF`), not warehouse **teal/navy** direction; WMS page does not use `PageTitleStyle` / card chrome consistently |
| Density | `Spacing.xaml`, card radii 16–22px | Comfortable for retail; WMS data worksheets feel **sparse** (ListView rows) vs Master Data |

**Opportunity:** Introduce a **WMS shell variant**: deep navy left rail, breadcrumb + context bar (warehouse · owner · operator), without removing retail shell.

---

## 2. Excel-style grids — Master Data (reference implementation)

| Capability | Master Data | Evidence |
|------------|-------------|----------|
| Virtualization / paging | Server-side pages + `RebuildSpreadsheet()` viewport sizing | `MasterDataPage.xaml.cs`, pagination buttons |
| Column resize / reorder | Custom spreadsheet host | Dynamic grid generation |
| Sort / filter | Per-column filters, catalog search | `MasterDataViewModel`, filter focus keys |
| Edit / paste | Edit mode, clipboard flows | Edit chrome, add row/column |
| Sticky header | Custom scroll host | `OnSpreadsheetScrollLoaded` |
| Read-only modes | Linked browse vs edit | `MasterDataSurfaceMode` |

**Conclusion:** PartFinder already has a **premium spreadsheet substrate** for templates/catalog. **Warehouse Workbook does not reuse it.**

---

## 3. Warehouse Workbook (`WmsWorkbookPage.xaml`)

### Layout

- Fixed **248px** left `ListView` nav; content in `ScrollViewer`.
- Page title + long subtitle + **horizontal toolbar** (warehouse combo, search, refresh ×2, export).
- Section visibility via `WmsSectionEqualsToVisibilityConverter` — one mega-page, many overlapping grids.

### Pain points

| Issue | Impact |
|-------|--------|
| **ListView-only worksheets** | No column headers, weak scanability, poor keyboard column navigation |
| **Toolbar proliferation** | Search vs “Refresh” vs “Refresh worksheet” — unclear mental model |
| **No persistent worksheet chrome** | Missing: Save/Cancel strip, filter chips, saved views, row count |
| **Dashboard minimal** | Text-only probe messages; no KPIs, tasks, or alerts |
| **Receiving workflow split** | Expected / goods / lines / QC / put-away are **separate nav items** — extra clicks vs guided flow |
| **Duplicate sections** | Put-away vs Location assignment show same data |
| **Validation feedback** | Mostly `StatusMessage` string; inline row errors (`ValidationError`) underused |
| **Posted vs draft** | Ledger/balances `IsEnabled="False"` — good — but draft grids visually similar to read-only lists |
| **Roadmap** | `WmsRoadmapPlaceholder` — clear for future phases; operational sections need stronger “live” vs “prototype” styling |

### Sections reviewed

| Section | UI pattern | Notes |
|---------|------------|-------|
| Dashboard | StackPanel text | Needs task inbox + health cards |
| Warehouse / locations / owners | Horizontal forms + ListView | Adequate for setup; needs master-detail |
| SKU registry | ListView + small form | Lacks identifier panel integration in grid |
| Stock balances / ledger | Paginated ListView, disabled | Correct read-only; needs enterprise grid |
| Receiving (03A) | Multi-row forms + ListView drafts | Recent POST wiring; still operator-unfriendly at scale |
| QC / disposition | NumberBox row | Disposition depends on line selection elsewhere |
| Administration | Grant email + checkboxes | No per-warehouse/owner matrix picker |

---

## 4. Legacy Inventory vs WMS inventory

| Surface | Role |
|---------|------|
| `InventoryPage` | Legacy `partfinder_inventory` (retail) |
| WMS balances/ledger | NestJS API, read-only |

**Risk:** Users may confuse **retail inventory** with **WMS authoritative stock**. UX must reinforce labels, icons, and color treatment (“posted / immutable”).

---

## 5. Reusable components and libraries

| Resource | Location |
|----------|----------|
| Design tokens | `Styles/Colors.xaml`, `Typography.xaml`, `Spacing.xaml`, `Controls.xaml` |
| Buttons | `PrimaryButtonStyle`, `GhostButtonStyle`, `OutlinedButtonStyle` |
| Loading | `LoadingSkeleton` (table kind used in Master Data) |
| MVVM | CommunityToolkit.Mvvm |
| Export | ClosedXML via `WmsWorksheetExportService` |
| Grid helper (WMS) | `WmsWorksheetGridHelper` — client sort/filter only |

**Not present:** WinUI `DataGrid` package, Syncfusion/Telerik, DevExpress.

---

## 6. Accessibility snapshot (current)

| Criterion | Master Data | WMS Workbook |
|-----------|-------------|--------------|
| Keyboard grid nav | Partial (custom) | **Weak** (ListView rows) |
| Focus visibility | Theme `BorderFocusColor` | Inconsistent on transparent nav buttons |
| Screen reader names | Some `AutomationProperties` in shell | **Sparse** on WMS lists |
| Touch targets | ≥44px on primary shell buttons | Receiving checkboxes **56px wide** — cramped |
| Error announcements | Status regions vary | Single `StatusMessage` text |

---

## 7. Workflow click-depth (estimated, current UI)

| Task | Approx. steps / clicks | Friction |
|------|------------------------|----------|
| Create expected receipt + lines | 5–8 + per line | No unified “document” sheet |
| Confirm receipt + QC partial | 6–10 across sections | Context switching |
| Put-away | 3–5 | Select task in one section, location in another |
| Check balance after receive | 2 + paginate | No drill-down from receipt line |

**Design target (UX-03):** Guided **Receiving hub** reducing cross-section navigation by 40% for happy path.

---

## 8. Audit summary — priority backlog for design (not implementation)

1. **Unify worksheet UX** with Master Data spreadsheet patterns (virtualized, sticky header).
2. **WMS visual lane**: navy sidebar + teal primary within existing token file structure.
3. **Context bar**: warehouse, owner, staging gate, operator role always visible.
4. **Task-oriented flows**: Receiving → QC → Put-away as a **stepper** with backlog.
5. **Prototype vs live** styling for roadmap modules (orders, picking, packing).
6. **Accessibility pass** on keyboard map and AutomationProperties before grid extraction.

---

*Next:* `UX-02-design-system.md`, `UX-03-screen-blueprints.md`, `UX-04-interaction-standards.md`, `UX-05-prototype-review.md`.