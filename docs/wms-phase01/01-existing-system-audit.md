# 01 — Existing System Audit (PartFinder.store)

**Audit date:** 2026-11-16  
**Scope:** Repository at repo root — WinUI desktop, NestJS backend, marketing portfolio.  
**Rule:** Items marked *verified* have matching source files; nothing is claimed without a path.

---

## 1. Repository and folder structure

| Area | Path | Role |
|------|------|------|
| Monorepo root | `CHANGELOG.md`, `version.json`, root `package.json` | Versioning; `npm run dev:backend` / `dev:portfolio` orchestration |
| Desktop app | `PartFinder/` — `PartFinder.sln`, `PartFinder/PartFinder/` (WinUI 3), `PartFinder.Core/`, `PartFinder.Tests/` | Primary operator UI; tenant Mongo access from client |
| License / setup API | `PartFinder-Backend/` — NestJS `src/app.module.ts` | Org licensing, setup wizard, invites, optional parts/templates API |
| Marketing / licence checkout | `Partfinder-Portfolio/` — Vite + React | Public site; proxy to backend for `/api` |
| CI / release | `.github/workflows/`, `PartFinder/RELEASING.md` | MSIX build; Release Please (optional) |

**Classification:** Structure is *existing and reusable* for adding a WMS workbook module in the desktop app and companion APIs. No WMS-specific folder exists today.

---

## 2. Frontend framework and routing

| Item | Detail | Classification |
|------|--------|----------------|
| Framework | **WinUI 3** (.NET 8, Windows App SDK) — `PartFinder/PartFinder/PartFinder.csproj` | *Existing and reusable* |
| Shell navigation | `Views/Components/ShellLayout.xaml`, `ViewModels/ShellViewModel.cs`, `Services/NavigationService.cs`, `Services/AppPage.cs` | *Existing and reusable* |
| Pages | Dashboard, Master Data, Templates, Parts, **Inventory** (“Warehouse”), Analytics, Linked Templates, QR, Alerts, Audit, Settings, User Management | *Existing*; Inventory is not full WMS |
| Setup / login | `MainWindow.xaml` / `MainWindow.xaml.cs` (4-step org setup, invite login) | *Existing and reusable* |
| Portfolio | React Router in `Partfinder-Portfolio/src/` | *Out of WMS operator scope* unless web RF is added later |

---

## 3. Backend framework and API structure

| Item | Path | Classification |
|------|------|----------------|
| NestJS app | `PartFinder-Backend/src/main.ts`, global prefix `api` | *Existing and reusable* |
| Setup (public) | `PartFinder-Backend/src/setup/setup.controller.ts` — status, DB, invite-login, change-password, forgot/reset password, refresh-session | *Existing* |
| Parts | `PartFinder-Backend/src/parts/` | *Existing but parallel* to desktop tenant data |
| Templates / relations / view-data | `templates/`, `relations/`, `view-data/` | *Existing* for catalog, not WMS |
| Import | `PartFinder-Backend/src/import/` — CSV jobs, Redis-backed state | *Existing but requires modification* for WMS bulk loads |
| Dashboard | `dashboard/` — JWT-guarded sample | *Existing but requires modification* |
| Auth (platform admin) | `auth/` — separate from org invite login | *Existing*; not org-operator WMS auth |

**Verified gap:** No `inventory`, `wms`, `orders`, or `shipments` modules under `PartFinder-Backend/src/`.

---

## 4. Database technology and schema

| Store | Technology | What lives there | Paths |
|-------|------------|------------------|-------|
| Platform DB | MongoDB (`MONGODB_URI`) | Organizations, licences, setup refresh sessions, parts/templates (API path), db-clusters | `organizations/schemas/organization.schema.ts`, `setup/schemas/` |
| Tenant DB | MongoDB per org (`Organization.orgDatabaseUri`) | Templates, master data rows, inventory list, audit, favourites, org users, Casbin rules (synced) | Desktop: `ILocalSetupContext`, `SetupPaths.cs`; Backend: `TenantMongoService` |
| Elasticsearch | Optional (`common/elasticsearch/`) | Parts search (backend) | *Existing but optional* |
| Redis | Optional (`common/redis/`) | Import job metadata | *Existing but optional* |

**Tenant collections (desktop — verified in services):**

| Collection | Service / file |
|------------|----------------|
| `partfinder_inventory` | `MongoInventoryService.cs` |
| `partfinder_master_data_rows` | `MongoMasterDataRecordsService.cs` |
| `partfinder_audit_logs` | `MongoAuditService.cs` |
| `user_sessions` | `MongoSessionService.cs` |
| Template / schema collections | `MongoTemplateSchemaService.cs` |

**Classification:** Per-org tenant isolation is *existing and reusable*. There is **no** centralized inventory ledger, order tables, or stock transaction log *verified* in code.

---

## 5. Authentication and authorization

| Capability | Implementation | Path | Classification |
|------------|----------------|------|----------------|
| Org invite login | Password + JWT access; optional refresh token + device id | `setup.service.ts` `validateInviteLogin`; `SetupApiClient.cs` | *Existing* |
| Desktop session | `AdminSessionStore` (local JWT file) | `Services/AdminSessionStore.cs` | *Existing* |
| Saved login | Credential Locker + refresh API | `SavedLoginCredentialStore.cs`, `QuickLoginService.cs` | *Existing* (requires deployed backend) |
| Portfolio sign-in | Same invite-login API | `AccountSignInPage.tsx` | *Existing* |
| Casbin | Model + sync on invite | `casbin/casbin.service.ts`, `rbac_model.conf` | *Existing but requires modification* — sync only; Nest routes largely unguarded by Casbin |
| Desktop RBAC | `UserAccessCapabilities` + `CurrentUserAccessService` | `UserAccessCapabilities.cs`, `CurrentUserAccessService.cs` | *Existing but requires modification* for WMS roles |
| Granular template/master permissions | On org user records | `MongoOrgUserDirectoryService.cs`, invite DTOs in backend | *Existing and reusable* pattern |

---

## 6. Company / tenant isolation

- **Model:** One **organisation** = 6-digit `orgCode` + dedicated Mongo URI (`organization.schema.ts` fields `orgCode`, `orgDatabaseUri`).
- **3PL multi–stock-owner companies within one warehouse:** *Not verified* — no `companyId` / `clientId` on inventory or master data.
- **Classification:** Single-tenant-per-customer is *existing*; multi-company-in-one-warehouse is *missing*.

---

## 7. Excel import/export and grid UX

| Feature | Path | Classification |
|---------|------|----------------|
| ClosedXML Excel | `ClosedXmlExcelTemplateService.cs`, `BulkInventoryImportService.cs`, `BulkTemplateImportService.cs` | *Existing and reusable* |
| Master Data grid | `MasterDataPage.xaml.cs` (large spreadsheet-style UI), `MasterDataViewModel.cs` | *Existing and reusable* for WMS workbook UX patterns |
| Template data grid | `TemplateDataPage.xaml`, `TemplateDataViewModel.cs` | *Existing and reusable* |
| Backend CSV import | `import/import.service.ts` | *Existing but requires modification* (CSV not Excel; job pattern differs from desktop) |
| Filters / pagination | `PaginatedViewModelBase`, `FilterBuilderControl.xaml`, inventory filters in `InventoryViewModel.cs` | *Existing and reusable* |

---

## 8. Existing inventory / warehouse features

| Feature | Verified? | Path |
|---------|-----------|------|
| Warehouse nav label | Yes | `InventoryViewModel.cs` — title “Warehouse Management” (admin) / “Warehouse” (employee) |
| Stock list CRUD | Yes | `MongoInventoryService.cs`, `InventoryDoc` (partId, quantity, minStock, **location string**, etc.) — **must remain** for non-cutover warehouses (D17, D28) |
| Bulk Excel import | Yes | `BulkInventoryImportService.cs` |
| KPI row / Order quick action | Yes | `InventoryPage.xaml`, `InventoryViewModel.cs` (UI only; no order backend) |
| Employee “Suggest” action | Yes | Same files — no verified order workflow behind it |
| Reservations / picking / dispatch | **No** | — |

**Classification:** Current warehouse feature = **lightweight parts inventory list**, not WMS.

---

## 9. Search, audit, notifications

| Item | Path | Classification |
|------|------|----------------|
| Parts / template search | Desktop services + optional ES on backend | *Partial* |
| Audit log UI | `AuditPage.xaml`, `MongoAuditService.cs`, `ActivityLogger.cs` | *Existing but requires modification* for WMS event types |
| Alerts page | `AlertsPage`, `MongoAlertsService.cs` | *Existing* — not WMS-specific |
| Email | Nodemailer in `setup.service.ts` (invites, password reset) | *Existing and reusable* for procurement alerts later |
| Background jobs | `import.service.ts` (`setImmediate` + Redis); `ActivityLogger` queue | *Existing but requires modification* — no durable WMS job queue |

---

## 10. QR, PDF, print

| Item | Path | Classification |
|------|------|----------------|
| QR generation | `QrCodeManagerViewModel.cs`, `QrCodeManagerPage.xaml` (QRCoder) | *Existing but requires modification* for package/location labels |
| PDF / shipping labels / print | No app implementation found (grep: no PdfSharp/QuestPDF/PrintManager usage) | *Missing* |

---

## 11. Automated tests and deployment

| Suite | Path | Classification |
|-------|------|----------------|
| Desktop unit tests | `PartFinder/PartFinder.Tests/` (6 files — templates, relations, grid) | *Existing* — no inventory/WMS tests |
| Backend e2e | `PartFinder-Backend/test/app.e2e-spec.ts` (Hello World only) | *Missing* depth |
| MSIX / GitHub releases | `PartFinder/build-msix-store.ps1`, workflows | *Existing* — desktop distribution |

---

## 12. Capability summary table

| Capability | Status | Primary paths |
|------------|--------|----------------|
| Spreadsheet workbook UX | Existing and reusable | `MasterDataPage`, `TemplateDataPage` |
| Tenant Mongo per org | Existing and reusable | `LocalSetupContext`, `organization.schema.ts` |
| User roles (Admin/Employee) | Existing and reusable | Org users + `UserAccessCapabilities` |
| Simple stock quantities | Existing and reusable | `partfinder_inventory` |
| Transactional stock ledger | Missing | — |
| Multi-company 3PL ownership | Missing | — |
| Orders / pick / pack / ship | Missing | — |
| Casbin enforced on APIs | Existing but requires modification | `casbin.service.ts` |
| Label / document printing | Missing | — |
| Concurrent task claiming | Missing | — |

---

## 13. Unknown / clarification

- Whether production tenants rely on **backend Parts API** or **only** desktop tenant Mongo (dual paths — see `MongoPartsDataService.cs` vs `parts.service.ts`).
- **Resolved (D01, D01b):** WMS **mutations** and scoped operational **reads** via NestJS only; legacy Inventory Mongo path unchanged until per-warehouse cutover.
- **WMS subscription fields:** `organization.schema.ts` has `plan`, `validity`, `status`, `maxUsers`, `maxParts` — **no** `wmsEnabled` or warehouse limits yet (D25 / B7).
- Hardware strategy for handheld scanning (WinUI only vs future web RF).