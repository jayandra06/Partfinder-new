# 02 — WMS Gap Analysis (19 modules)

Maps each target module to **Existing (E)**, **Modify (M)**, **Missing (D)**, or **Unknown (U)**. Paths refer to `01-existing-system-audit.md`.

**Coexistence:** New **Warehouse Management Workbook** (D16) does not replace legacy **Inventory** or Retail (D03, D28). Decisions: `06-wms-risks-and-questions.md`.

---

## Module matrix

| # | Module | Status | Reuse / notes |
|---|--------|--------|----------------|
| 1 | Company & Warehouse Setup | **D** | Org = license tenant only (`organization.schema.ts`). No warehouse entity, zones, or sites. |
| 2 | Users & Permissions | **M** | `org_app_users`, `UserAccessCapabilities`, Casbin sync — extend for WMS roles (picker, packer, QC). |
| 3 | Master Data | **E** | `MasterDataViewModel`, `partfinder_master_data_rows` — parts/SKU reference data for WMS. |
| 4 | Excel Data Management | **E** | ClosedXML import/export; `BulkInventoryImportService` pattern for WMS worksheets. |
| 5 | Inbound Planning | **D** | No ASN/PO expectation tables. |
| 6 | Goods Receiving | **D** | No receiving headers/lines vs inventory receipts. |
| 7 | Quality Control | **D** | No hold/quarantine workflow. |
| 8 | Put-away & Storage | **D** | Only free-text `location` on `InventoryDoc`. |
| 9 | Inventory Management | **M** | `partfinder_inventory` is snapshot qty, not ledger-backed. |
| 10 | Internal Stock Movements | **D** | No transfer/adjustment transactions. |
| 11 | Stock Counting & Audits | **M** | `AuditPage` + `MongoAuditService` for events; no cycle-count worksheets. |
| 12 | Outbound Order Management | **D** | No sales/pick orders (except licence `orderId` in payment). |
| 13 | Picking Operations | **D** | No tasks, waves, or split picks. |
| 14 | Packing Operations | **D** | No packages, scan-verify, discrepancies. |
| 15 | Dispatch & Shipping | **D** | No shipment confirm or carrier integration. |
| 16 | Returns & Exceptions | **D** | — |
| 17 | Warehouse Administration | **M** | Settings, user mgmt partial; no WMS config (reorder rules, label templates). |
| 18 | Reports & Analytics | **M** | `DashboardViewModel`, backend `dashboard` — no WMS KPIs. |
| 19 | 3PL Client Management & Labels | **D** | No client companies; QR only for parts (`QrCodeManagerViewModel`). |

---

## Critical workflows (Section 4) — gap summary

| Workflow | Today | Gap |
|----------|-------|-----|
| A. Multi-employee simultaneous picks | Single desktop sessions; no task queue | **D** — optimistic locking + task assignment service |
| B. Multiple stock-owning companies | Single org per DB | **D** — `stock_owner_id` / client master + ownership on balances |
| C. Reserve → pick → stage | Direct qty on document | **D** — reservation + staged status in ledger |
| D. Packing verification | — | **D** — package lines, scan confirm, exception approval |
| E. Printable documents | — | **D** — template engine + print queue |
| F. Dispatch-only deduction | Edits change `InventoryDoc.quantity` directly | **M/D** — replace with ledger `on_hand` minus reservations; dispatch posts |
| G. Procurement / replenishment | `minStock` field only | **M** — reorder calc + notifications (reuse `MongoAlertsService` / email) |

---

## What can be reused without change (Phase 02+)

- Shell navigation pattern (`ShellViewModel`, new `AppPage.WmsWorkbook` or sub-nav).
- Master Data grid patterns for WMS worksheets.
- `BulkInventoryImportService` / ClosedXML for bulk IO.
- Tenant connection pattern (`MongoInventoryService.TryGetCollection` style).
- Invite login, org isolation, audit logging plumbing.
- Activity / audit UI shell (`AuditPage`).

---

## What must not be broken

- Existing Master Data, Templates, Parts, Analytics, Linked Templates.
- Current `partfinder_inventory` consumers (`InventoryPage`) — migrate via compatibility layer or parallel collections.
- Setup wizard and licence verification flows.
- Production tenant data — additive schema only (new collections).