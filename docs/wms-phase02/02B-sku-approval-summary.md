# SKU architecture — product owner approval summary

**Version:** 2026-11-17 (revised after codebase audit)  
**Blocks:** Live `wms_stock_balances`, opening-balance post, receiving mutations.

---

## Decision: immutable WMS SKU registry

| Question | Proposed answer |
|----------|-----------------|
| What is the stock key? | **`skuId`** (ULID) in `wms_skus` — stored as `skuKey` on balances/ledger |
| Is master data `_id` the stock key? | **No** — imports create new IDs; rows are per-template and deletable |
| Is Parts `rowId` the stock key? | **No** — use as **cross-reference** only (`partsRecord` namespace) |
| Duplicate part numbers? | **Do not auto-merge** — namespace-scoped uniqueness + ambiguity report |
| Customer PN? | **Order line only** (Phase 03), not on SKU registry |
| 3PL? | **`ownerId`** on balances; product = `skuId` |

---

## Identifier map (PO view)

| # | Identifier | Where stored | Notes |
|---|------------|--------------|-------|
| 1 | Internal WMS SKU | `wms_skus.skuId` | Canonical |
| 2 | Master data row | `wms_sku_identifiers` | `templateId` required in metadata |
| 3 | Parts record | `wms_sku_identifiers` | Nest `parts` or WinUI catalog link |
| 4 | Manufacturer + MPN | `wms_sku_identifiers` | Pair required for uniqueness |
| 5 | Supplier PN | `wms_sku_identifiers` | Per supplier |
| 6 | Customer PN | Order line field | Not SKU |
| 7 | ERP material | `wms_sku_identifiers` | Per `erpConnectorId` |
| 8 | Barcode / QR | `wms_sku_identifiers` | Symbology + value |
| 9 | Variant / UoM | `wms_skus.variantGroupId`, `primaryUom` | Variants may share group but not stock without rule |

---

## Duplicate and ambiguous handling

| Scenario | System behaviour |
|----------|-------------------|
| Same MPN, two manufacturers | Two SKUs unless PO approves merge |
| Re-imported Excel row (new `_id`) | Suspected duplicate workflow — **no** auto-link |
| Two master rows, same description | Flag in dry-run; human maps to one `skuId` or two |
| ERP material maps to two SKUs | Reconciliation report; block inbound until resolved |

---

## Migration from legacy inventory

- Dry-run maps `InventoryDoc` → candidate `skuId` (by `partId` string match to identifier, else manual).
- **No** production balance until PO approves registry rules + sample tenant dry-run results.

---

## Approval checklist

| # | Item | Approve (Y/N) | Notes |
|---|------|---------------|-------|
| 1 | ULID `wms_skus` registry | | |
| 2 | Master data as cross-ref only | | |
| 3 | No PN-only auto-merge | | |
| 4 | MPN uniqueness = mfr + mpn | | |
| 5 | Customer PN on order lines only | | |
| 6 | Merge/retire SKU controlled process | | |
| 7 | UoM on SKU (`primaryUom`) | | Name master column: _________ |

**Signed:** _________________ **Date:** _________