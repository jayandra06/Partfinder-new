# WMS SKU identity — canonical mapping (revised)

**Status:** Design for PO approval — **replaces** interim “`skuKey` = master data `_id`” rule.  
**Product summary:** `02B-sku-approval-summary.md`  
**Code basis:** `MongoMasterDataRecordsService.cs`, `part.schema.ts`, `InventoryDoc` in `MongoInventoryService.cs`

---

## Why master data row ID is not the WMS SKU

| Code fact | Implication |
|-----------|-------------|
| `AppendRowsAsync` always uses `ObjectId.GenerateNewId()` | Excel/CSV **import appends new rows** with **new** IDs |
| `SaveRowsAsync` / `UpsertRowAsync` keep ID only when client sends existing `rowId` | IDs are stable only while that row document survives |
| Rows are scoped by `templateId` | The **same logical product** can appear as **different rows** in different templates/worksheets |
| `DeleteRowAsync`, template clear, re-import | Rows can be **deleted and recreated** with new IDs |
| Legacy `partfinder_inventory` uses free-text `partName`, optional `partId` string | **No** guaranteed link to master data ObjectId |
| Backend `parts` collection uses `orgId` + `templateId` + `rowId` | Part identity is **catalog-relative**, not a global WMS key |

**Conclusion:** Use an immutable **`wms_skus.skuId`** (ULID) as the only key on balances and ledger lines (`skuKey` field stores `skuId`).

---

## Canonical registry: `wms_skus`

| Field | Purpose |
|-------|---------|
| `skuId` | Immutable ULID (this is `skuKey` everywhere in WMS) |
| `status` | `active` \| `merged` \| `retired` |
| `mergedIntoSkuId` | When merged, all new postings target survivor |
| `primaryUom` | Base unit of measure for ledger (PO picks column source) |
| `variantGroupId` | Optional — links size/color variants without merging stock |
| `createdAt` / `updatedAt` | Audit |

**Do not** copy full master grid into `wms_skus`. Display attributes remain in master data / parts; WMS holds identity + links only.

---

## Cross-references: `wms_sku_identifiers`

One SKU may have many external identifiers. Uniqueness is **per namespace**, not global.

| `namespace` | Example `value` | Uniqueness scope |
|-------------|-----------------|------------------|
| `masterDataRow` | Mongo `_id` string | One active link per `(templateId, rowId)` |
| `partsRecord` | Backend `parts._id` or composite `templateId+rowId` | Per org catalog row |
| `manufacturerPn` | `MPN-123` | `(manufacturerName, mpn)` — **same MPN, different mfr = different SKUs** |
| `supplierPn` | `SUP-9` | `(supplierId or supplier name, spn)` |
| `erpMaterial` | `MAT-001` | `(erpConnectorId, materialId)` |
| `barcode` | Code 128 / GTIN | `(symbology, value)` |
| `legacyInventory` | `partfinder_inventory` mongo id | Dry-run / migration only |

**Rules**

- **No auto-merge** on matching MPN or barcode alone — create **ambiguous match report** for PO/ops.
- Customer-requested PN lives on **order lines** (Phase 03), namespace `customerPn` optional on line only.
- Multiple suppliers for one product → multiple `supplierPn` rows, **one** `skuId`.

---

## Linking workflow (02B implementation boundary)

1. **Register SKU** — create `wms_skus` + optional initial identifiers (admin API).
2. **Link master row** — add `masterDataRow` identifier when user confirms mapping.
3. **Re-import detection** — if new master row matches heuristics but different `_id`, flag **suspected duplicate**; do not relink balances automatically.
4. **Merge** — controlled `merged` status + `mergedIntoSkuId`; **no** silent balance consolidation without transaction + approval.

---

## 3PL ownership (unchanged principle)

- `skuId` = **what** the product is.
- `ownerId` on balances = **who** owns the stock (see `wms_stock_owners`).
- Same `skuId` + different `ownerId` = separate balance rows (AT-2).

---

## Opening balance / legacy migration

| Legacy source | Rule |
|---------------|------|
| `partfinder_inventory` | Resolve to `skuId` via rules + manual review queue |
| Unresolved lines | Report only — **no** ledger post |
| New WMS receipts (02B+) | Require registered `skuId` |

---

## PO sign-off

See checklist in `02B-sku-approval-summary.md`. **No** `wms_stock_balances` until signed.