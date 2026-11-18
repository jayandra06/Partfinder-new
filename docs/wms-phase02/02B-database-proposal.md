# Phase 02B — Database proposal (additive)

**Extends:** `docs/wms-phase01/04-wms-database-proposal.md`  
**Constraint:** No changes to `partfinder_master_data_rows`, `partfinder_inventory`, or legacy collections.

---

## 1. SKU registry

### `wms_skus`

| Field | Type | Index |
|-------|------|-------|
| `_id` / `skuId` | string (ULID) | unique |
| `status` | enum | |
| `mergedIntoSkuId` | string? | |
| `primaryUom` | string | |
| `variantGroupId` | string? | |
| `createdAt`, `updatedAt` | date | |

### `wms_sku_identifiers`

| Field | Type | Index |
|-------|------|-------|
| `skuId` | string | |
| `namespace` | string | compound unique per rules below |
| `value` | string | |
| `metadata` | object | `templateId`, `manufacturerName`, `supplierId`, `erpConnectorId`, `symbology` |
| `status` | active \| superseded | |

**Uniqueness (examples)**

- `masterDataRow`: unique `(templateId, rowId)` among active rows  
- `manufacturerPn`: unique `(manufacturerName, value)` among active  
- `erpMaterial`: unique `(erpConnectorId, value)` among active  

---

## 2. Balances & ledger

### `wms_stock_balances`

| Field | Type |
|-------|------|
| `ownerId` | ObjectId |
| `warehouseId` | ObjectId |
| `locationId` | ObjectId |
| `skuKey` | string (= `skuId`) |
| `lotId`, `serialId` | string? |
| `onHand` | decimal |
| `reserved` | decimal |
| `blocked` | bool or qty flag per PO |
| `version` | int |
| `updatedAt` | date |

**Unique:** compound key in §2 of `02B-technical-specification.md`.

### `wms_stock_transactions`

| Field | Type |
|-------|------|
| `txnId` | ULID |
| `type` | enum |
| `ownerId` | ObjectId |
| `warehouseId` | ObjectId |
| `lines[]` | skuKey, qtyDelta, fromLoc, toLoc, lot, serial |
| `refs` | orderId, packageId, external refs |
| `idempotencyKey` | string? |
| `reversalOf` | string? |
| `createdBy` | email |
| `createdAt` | date |

Append-only.

### `wms_idempotency_keys`

| Field | Type |
|-------|------|
| `key` | string unique |
| `resultHash` | string |
| `expiresAt` | date |

---

## 3. ERP (schema only in 02B)

- `wms_erp_connectors`
- `wms_erp_sku_mappings`
- `wms_erp_location_mappings`
- `wms_inbound_messages`
- `wms_outbox_events`

See `02B-technical-specification.md` §6.

---

## 4. 3PL segregation

All balance and ledger lines **require** `ownerId`.  
`ownerTransfer` transaction type is the only automated cross-owner move without dispatch semantics.

---

## 5. Migration

- No automatic creation of balances from master data import.  
- Dry-run job outputs proposed `openingBalance` lines + ambiguous SKU report.  
- Legacy `partfinder_inventory` remains until per-warehouse cutover (D17).