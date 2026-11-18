# Phase 02A — Implementation report

**Date:** 2026-11-17  
**Scope:** Setup masters only — **no stock mutations**, ledger, pick, pack, dispatch.

---

## 1. Files created / modified

### Backend (`PartFinder-Backend/src/wms/`)

| File | Role |
|------|------|
| `wms.module.ts` | Module registration |
| `wms-setup.controller.ts` | `/api/wms/v1/*` routes |
| `guards/wms-session.guard.ts` | JWT + org + licence + WMS entitlement + tenant user |
| `services/wms-setup.service.ts` | CRUD warehouses, owners, zones, locations |
| `services/wms-tenant-collections.service.ts` | Tenant collections + indexes |
| `services/wms-diagnostics.service.ts` | Transaction probe |
| `dto/*.ts` | Validated request bodies |
| `guards/wms-session.guard.spec.ts` | Unit test (invalid org header) |

**Also modified:** `app.module.ts`, `organization.schema.ts` (`wmsEnabled`, limits), `organizations.service.ts` (`verifyLicense`), `setup.service.ts` (JWT includes `orgCode`), `auth/jwt.strategy.ts` (payload type).

### Desktop (`PartFinder/PartFinder/`)

| File | Role |
|------|------|
| `Services/WmsApiClient.cs` | API client |
| `ViewModels/WmsWorkbookViewModel.cs` | Setup UI logic |
| `Views/Pages/WmsWorkbookPage.xaml(.cs)` | Workbook UI |
| `AppPage.WmsWorkbook`, `NavigationService`, `ShellViewModel` | Nav **WMS Setup** |
| `UserAccessCapabilities.CanAccessWmsWorkbook` | Admin-only nav |

---

## 2. API routes (prefix `https://<host>/api/wms/v1`)

| Method | Path | Auth | Notes |
|--------|------|------|-------|
| GET | `/health` | JWT + WmsSessionGuard | Phase marker |
| GET | `/diagnostics/transactions` | same | Mongo txn probe |
| GET/POST/PATCH | `/warehouses` | same | Admin write |
| GET/POST/PATCH | `/stock-owners` | same | Admin write |
| GET/POST/PATCH | `/zones` | same | Requires `warehouseId` query on GET |
| GET/POST/PATCH | `/locations` | same | Requires `warehouseId` query on GET |

**Headers:** `Authorization: Bearer <accessToken>`, `X-Org-Id: <6-digit orgCode>`.

---

## 3. Tenant resolution & authorization

1. **JWT** validated by `AuthGuard('jwt')` (`email`, optional `orgCode`).
2. **`X-Org-Id`** must be 6-digit org code; must match JWT `orgCode` when present.
3. **`verifyLicense`** — active subscription, not banned.
4. **`wmsEnabled`** on organization (**opt-in** — `true` only when sold; see 02A.1).
5. User must exist in tenant **`org_admin_users`** or **`org_app_users`** for that org DB.
6. **Writes** require **Admin** role (`isWmsAdmin`).
7. **Reads** filter warehouses/owners by `wms_operator_grants` when not admin (collection ready; grants UI later).

Tenant URI is **never** returned to the client.

---

## 4. Tenant Mongo collections (additive)

| Collection | Unique index |
|------------|----------------|
| `wms_warehouses` | `code` |
| `wms_stock_owners` | `code` |
| `wms_zones` | `warehouseId` + `code` |
| `wms_locations` | `warehouseId` + `path` |
| `wms_operator_grants` | (optional) `email` |

**No** `wms_stock_balances`, transactions, orders, or reservations.

---

## 5. Transaction topology

Run from app: **WMS Setup** → transaction probe line after refresh, or:

`GET /api/wms/v1/diagnostics/transactions`

**Result:** Depends on tenant Mongo deployment (replica set required for production ledger). Record outcome per environment in ops notes.

---

## 6. Manual test steps

1. Start backend: `cd PartFinder-Backend && npm run start:dev`
2. Ensure `.env` has `JWT_SECRET`, `MONGODB_URI`, org has `orgDatabaseUri`.
3. Login in WinUI with org that has API base URL pointing to local/staging backend.
4. Open **WMS Setup** in nav (admin).
5. Create warehouse `WH01`, stock owner `CLIENT1`, zone and location for selected warehouse.
6. Confirm legacy **Warehouse Management** (Inventory) still edits `partfinder_inventory`.
7. Call API without token → 401; wrong `X-Org-Id` vs JWT → 403.

**Build:** `npx nest build` (backend). Desktop: build WinUI solution as usual.

---

## 7. Blockers before Phase 02B

| ID | Item |
|----|------|
| B1 | Mongo multi-doc transactions verified on staging |
| B6 | SKU identity sign-off |
| B7 | WMS entitlement in production org records (fields exist; set `wmsEnabled` per plan) |

---

## 8. Phase 02B proposal (not started)

- Append-only ledger collections, posting service inside **transactions** only.
- Read APIs for balances; still no WinUI stock Mongo writes.
- Opening balance dry-run tool only after B1+B6.

**STOP:** No ledger/reserve/pick/pack/dispatch in this delivery.

---

## 9. Gate sign-off

**Formal acceptance:** `02A-gate-signoff.md` (manual tests + B1–B7 record).  
**B6 interim doc:** `02B-sku-key-mapping.md` (PO sign-off required before live balances).