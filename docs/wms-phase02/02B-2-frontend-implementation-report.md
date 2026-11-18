# Phase 02B.2 — Frontend Implementation Report

**Scope:** Warehouse Workbook WinUI shell, operational worksheets, roadmap placeholders.  
**Out of scope:** Production/staging inventory posting UI, Retail Workbook changes, Phase 03 operations.

---

## Screens created and modified

| Asset | Change |
|-------|--------|
| `Views/Pages/WmsWorkbookPage.xaml` | Left-rail navigation + section worksheets (Excel-style row grids) |
| `ViewModels/WmsWorkbookViewModel.cs` | Warehouse filters, SKU/UOM draft fields, catalog summary |
| `ViewModels/WmsWorkbookViewModel.Navigation.cs` | Section catalog, roadmap vs operational routing |
| `ViewModels/WmsWorkbookViewModel.SkuAndInventory.cs` | SKU, identifier, UOM, dry-run loads |
| `Services/WmsApiClient.cs` | SKU CRUD, identifiers, UOM, catalog status, PATCH |
| `ViewModels/ShellViewModel.cs` | Nav label **Warehouse Workbook** |
| `Helpers/ApiErrorFormatter.cs` | WMS forbidden / subscription messaging |
| `Views/Components/WmsRoadmapPlaceholder.xaml` | Reusable roadmap panel |
| `Converters/WmsSectionEqualsToVisibilityConverter.cs` | Section switching |
| `Models/WmsWorkbookSections.cs` | Section id constants |
| `Models/WmsFutureModuleRegistry.cs` | Phase 03+ placeholder descriptors (no ops) |
| `PartFinder.Tests/WmsWorkbookFilterTests.cs` | Search filter unit test |

**Unchanged:** Retail pages, legacy `InventoryPage`, Master Data, Templates navigation.

---

## Working worksheet functions

| Worksheet | Read | Create/update (admin) |
|-----------|------|------------------------|
| Dashboard | Health, txn probe, phase | — |
| Warehouse master | List | Create warehouse |
| Zones & locations | List per warehouse | Create zone, location |
| 3PL stock owners | List | Create owner |
| SKU registry | List, select | Register SKU (save after API OK) |
| SKU identifier mapping | List for selected SKU | Add identifier (+ pre-check) |
| Units of measure | List standard + custom | Create custom UOM |
| Stock balances | Paginated, filter owner/location | **Read-only** |
| Location stock | Balance rows by location | **Read-only** |
| Movement ledger | Paginated transactions | **Read-only** |
| Opening balance preview | Dry-run summary | **Read-only** |
| SKU mapping errors | Summary + unmapped rows | **Read-only** |
| PartFinder master data | Guidance text | Use main app Master Data |
| Administration | Phase, probe | **Read-only** |

Global: warehouse selector, search, refresh, role gate via `CanEditSetup` (org admin).

---

## Backend APIs connected

| API | WinUI use |
|-----|-----------|
| `GET /api/wms/v1/health` | Dashboard |
| `GET /api/wms/v1/diagnostics/transactions` | Dashboard / Admin |
| `GET /api/wms/v1/catalog/status` | Phase label |
| `GET|POST /api/wms/v1/warehouses` | Warehouse master |
| `GET|POST /api/wms/v1/stock-owners` | Owners |
| `GET|POST /api/wms/v1/zones`, `locations` | Locations |
| `GET|POST /api/wms/v1/skus`, `PATCH …/skus/:id` | SKU registry |
| `GET /api/wms/v1/skus/:id` | Identifiers |
| `POST …/identifiers`, `…/identifiers/check` | Mapping |
| `GET|POST /api/wms/v1/uom` | UOM |
| `GET /api/wms/v1/inventory/balances` | Balances, location stock |
| `GET /api/wms/v1/inventory/transactions` | Ledger |
| `GET /api/wms/v1/ledger/opening-balance-dry-run` | Opening preview, errors |

**Not wired in UI:** `POST /api/wms/v1/inventory/post` (by design).

---

## Screens disabled (roadmap)

Operational flag **false** — `WmsRoadmapPlaceholder` with phase label:

| Section | Phase |
|---------|-------|
| Receiving | 03 |
| Orders | 03 |
| Picking | 03 |
| Packing | 04 |
| Printing | 04 |
| Dispatch | 04 |
| Procurement | 05 |
| Reports | 05 |

---

## Build and test results

```text
dotnet build PartFinder/PartFinder.csproj -c Debug -p:Platform=x64
# Build succeeded (0 errors; pre-existing warnings elsewhere)

dotnet test PartFinder.Tests/PartFinder.Tests.csproj -c Debug -p:Platform=x64
# Includes WmsWorkbookFilterTests

npm test -- --testPathPatterns=wms   (PartFinder-Backend)
# 9 suites, 29 tests passed (backend WMS; unchanged by 02B.2)
```

### Not executed

- Full WinUI manual UI test pass against live pilot org
- Employee cross-owner isolation E2E (backend enforced; UI relies on API errors)
- Legacy Inventory / Retail regression automation
- Excel export for WMS worksheets (not in API scope for 02B.2)

---

## Manual test instructions

1. Sign in as org **admin** with `wmsEnabled: true` on backend.
2. Open **Warehouse Workbook** from shell navigation.
3. **Dashboard** — confirm health message and transaction probe text.
4. **Warehouse master** — create warehouse; verify row appears after success message only.
5. Select warehouse → **Zones & locations** — add zone and location path.
6. **Stock owners** — create 3PL owner.
7. **SKU registry** — Save new SKU; confirm list refresh.
8. Select SKU → **SKU identifier mapping** — add MPN; test duplicate shows conflict message.
9. **Units of measure** — list loads; optional custom UOM for admin.
10. **Stock balances** — filter owner/location; ledger pages read-only.
11. **Opening balance preview** / **Mapping errors** — run as admin; no legacy Mongo edits.
12. Click **Receiving** (roadmap) — placeholder only, no actions.
13. Non-admin user — create buttons disabled; API calls still guarded server-side.

---

## Known limitations

- Grids use `ListView` row templates (not full ClosedXML export for WMS worksheets).
- Identifier metadata (templateId, erpConnectorId) edited via minimal fields; advanced metadata JSON not in UI.
- Column resize / Excel paste for bulk SKU import — deferred.
- Location hierarchy (aisle/rack/shelf) modeled as zone + path string (matches 02A backend).
- Search is client filter + server sku filter on balances when search non-empty.

---

## Requirements for Phase 03

- Order/receiving APIs and reservation model before enabling Operations nav items.
- Reuse `WmsRoadmapPlaceholder` + `WmsFutureModuleRegistry` for new worksheets.
- Optional: shared `TablePaginationBar` binding on balance/ledger view models.
- Picking/packing must remain disabled until matching NestJS endpoints exist.
- No staging **inventory post** controls in production WinUI without separate authorization.

---

**Stop:** Phase 02B.2 complete. No production posting, reservations, pick/pack/dispatch implementation.