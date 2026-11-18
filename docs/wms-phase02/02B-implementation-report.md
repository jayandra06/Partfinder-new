# Phase 02B — Implementation Report (controlled dev)

**Status:** Backend foundation implemented on branch `dhoni/wms`.  
**Not enabled:** production stock posting, warehouse cutover, live ERP sync.

## Scope delivered

| Area | Implementation |
|------|----------------|
| SKU registry | `wms_skus` — ULID `skuId`, `primaryUom`, variant fields, merge/retire |
| Identifiers | `wms_sku_identifiers` — scoped `scopeKey`, partial unique index on active rows |
| Audit | `wms_sku_audit_log` — create/update/identifier/merge/retire |
| UOM | Standard codes + `wms_uom_definitions` custom units |
| Excel UOM map | `wms_uom_template_mappings` + resolve API (no bulk import yet) |
| Ledger | Dry-run only (`GET /api/wms/v1/ledger/opening-balance-dry-run`) |
| ERP foundation | Connector CRUD stub + inbound idempotency stub (no outbound/live) |

## API surface (`WmsPhase02Controller`, JWT + `WmsSessionGuard`)

- `GET /api/wms/v1/catalog/status` — `phase: 02B`, `liveStockPosting` from env
- `GET|POST /api/wms/v1/uom`
- `GET|POST /api/wms/v1/uom/template-mappings`
- `POST /api/wms/v1/uom/resolve-template`
- `GET|POST /api/wms/v1/skus`, `GET|PATCH /api/wms/v1/skus/:skuId`
- `POST /api/wms/v1/skus/:skuId/identifiers`
- `POST /api/wms/v1/skus/identifiers/check`
- `POST /api/wms/v1/skus/:skuId/merge` | `retire`
- `GET /api/wms/v1/ledger/opening-balance-dry-run`
- `GET|POST /api/wms/v1/erp/connectors`, `POST /api/wms/v1/erp/inbound/stub`

Setup health remains at `GET /api/wms/v1/health` with `phase: 02B`, `stockMutations: false`.

## Safety gates

- `WMS_LIVE_STOCK_POSTING` must be exactly `'true'` for live posting (default off). No balance/transaction writers shipped in 02B.
- SKU merge **does not** rewrite ledger history (merge marks identifiers superseded; ledger collections not written).
- Identifier conflicts return `409` with existing `skuId`; check endpoint surfaces ambiguity hints.

## Indexes

`WmsTenantCollectionsService.ensurePhase02Indexes()` creates Phase 02A + 02B indexes on first SKU/UOM/ERP call per tenant.

## Tests

- Existing 02A.1 WMS security/entitlement specs
- New `wms-sku-namespaces.spec.ts` (scope key rules)

## Deferred (per authorization)

- WinUI SKU workbook / Excel bulk SKU create
- `wms_stock_balances` / `wms_stock_transactions` posting services
- Live ERP connectors and outbox dispatch
- Production opening-balance migration

## Dependencies

- `ulid` package for `skuId` and `erpConnectorId`