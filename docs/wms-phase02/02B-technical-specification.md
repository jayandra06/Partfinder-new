# Phase 02B — Technical specification

**Scope:** SKU registry, ledger, balances, dry-run, ERP **interfaces** (no live connectors).  
**Out of scope:** Production stock activation, pick/pack/dispatch UI, live ERP sync.  
**Prerequisites:** PO signs `02B-sku-approval-summary.md`; B1 txn evidence per tenant.

---

## 1. SKU layer (02B.0 — before balances)

| Collection | Purpose |
|------------|---------|
| `wms_skus` | Immutable `skuId` (ULID) = `skuKey` on ledger |
| `wms_sku_identifiers` | Cross-refs: master data, parts, MPN, ERP, barcode (see `02B-sku-key-mapping.md`) |

**APIs:** Register SKU, add identifier, retire/merge SKU (admin, audited). **No** balance writes until SKU exists.

---

## 2. Quantity states (no double-counting)

| State | Meaning | Storage |
|-------|---------|---------|
| **Physical on-hand** | Units physically in warehouse custody at a **location** | `wms_stock_balances.onHand` (location-level) |
| **Reserved** | Committed to orders, still on-hand | `wms_stock_balances.reserved` and/or `wms_reservations` (Phase 03) |
| **Picked / staged** | Moved to staging location type; **on-hand unchanged** in total | Location transfer: storage → `staging` location; same `onHand` sum |
| **Packed** | Inside sealed package contents; still on-hand until dispatch | `wms_package_contents` (Phase 04) |
| **Available (ATP)** | **Computed only** | `onHand - reserved - blocked` (server); never a separate physical column |
| **Quarantined / blocked** | On-hand but not available | Balance flag or `quarantine` location type |

**Invariants**

- Sum of `onHand` across locations = physical custody (per owner × warehouse × skuId).
- Pick/pack **never** reduce total physical on-hand (only move location or sub-state).
- **Dispatch** reduces `onHand` at dispatch location by shipped qty only.
- Adjustments / write-offs use txn type `adjust` or `writeOff` with approval metadata.

---

## 3. Ledger collections

| Collection | Role |
|------------|------|
| `wms_stock_balances` | Materialized view; updated only inside posting txn |
| `wms_stock_transactions` | Append-only lines; `reversalOf` for controlled reversals |
| `wms_idempotency_keys` | Posting + inbound dedup |

### Balance key

`{ ownerId, warehouseId, locationId, skuKey (skuId), lotId?, serialId? }`

### Transaction types (02B subset)

| Type | Physical on-hand | Reserved | Notes |
|------|------------------|----------|-------|
| `openingBalance` | +Δ (dry-run first) | — | 02B dry-run only until PO |
| `receive` | +Δ | — | Future inbound |
| `move` | −from, +to | — | Same total |
| `adjust` | +/−Δ | — | Approval required |
| `writeOff` | −Δ | — | Approval required |
| `reversal` | Compensates prior txn | — | Links `reversalOf` |
| `ownerTransfer` | −Δ owner A, +Δ owner B | — | **Only** path to move stock between 3PL owners |

`reserve`, `unreserve`, `pick`, `pack`, `dispatch` — **specified here**, implemented Phase 03–04 (02B documents types only).

---

## 4. Posting service

| Concern | Design |
|---------|--------|
| Entry | Single `PostingService.post(command)` |
| Atomicity | Mongo `session.withTransaction` — **fail closed** if unsupported |
| Concurrency | Optimistic `version` on balance doc; retry bounded |
| Idempotency | `idempotencyKey` stored in `wms_idempotency_keys`; replay returns same result |
| Immutability | No update/delete on `wms_stock_transactions`; reversals append |
| Authorization | `WmsSessionGuard` + owner/warehouse scope + admin roles per txn type |

---

## 5. Read APIs (02B)

- Balances by warehouse/owner/location/skuId.
- Ledger history filtered + paginated.
- ATP endpoint (computed).
- SKU registry + identifier search.
- Opening-balance **dry-run** report (no write).

WinUI: read via API only; no Mongo stock truth.

---

## 6. ERP integration readiness (canonical interface — no live connectors)

**Principle:** ERP is never a second authority for physical on-hand. WMS ledger owns custody; ERP receives **events** and may send **commands** that WMS validates and posts.

### 6.1 Connector registry (`wms_erp_connectors`)

| Field | Notes |
|-------|-------|
| `erpConnectorId` | ULID |
| `connectorType` | `sap` \| `oracle` \| `dynamics` \| `netsuite` \| `odoo` \| `custom` \| `manual` |
| `ownerId` | 3PL scope when connector is client-specific |
| `authProfileRef` | Secret ref only — not in tenant doc |
| `isActive` | Kill switch |

### 6.2 Mappings

| Table | Maps |
|-------|------|
| `wms_erp_sku_mappings` | external material → `skuId` |
| `wms_erp_location_mappings` | external wh/loc → WMS ids |
| `wms_sku_identifiers` | Also holds `erpMaterial` namespace |

Prefer **one** internal `skuId`; many ERP material rows may map after PO review.

### 6.3 Inbound messages (`wms_inbound_messages`)

Headers: `erpConnectorId`, `messageType`, `idempotencyKey`, `externalDocumentId`.

States: `received` → `validated` → `processed` \| `failed` \| `deadLetter`.

02B: schema + stub handler that **validates only** (no stock post).

### 6.4 Outbox (`wms_outbox_events`)

Written in **same transaction** as ledger post when integration enabled.

Event types (examples): `stockAdjusted`, `dispatchConfirmed`, `receiptPosted`.

Publisher worker — Phase 05+; 02B stores rows only.

### 6.5 Sync & reconciliation

| Field | Purpose |
|-------|---------|
| `lastAckAt` | ERP acknowledged event |
| `reconciliationRunId` | Compare ERP snapshot vs WMS balances (read-only report) |

Failed messages: exponential backoff, max attempts, operator retry UI later.

---

## 7. Security

02A.1: JWT `orgCode` authoritative, `wmsEnabled === true`, operator grants, cross-owner blocked without `ownerTransfer`.

---

## 8. Phase 02B implementation boundaries

| In 02B | Not in 02B |
|--------|------------|
| `wms_skus`, `wms_sku_identifiers` CRUD | Live opening balance post to prod |
| Balance + ledger **schema** + posting service **behind internal flag** | Reservation/pick/pack/dispatch mutations |
| Dry-run opening balance | ERP live connectors |
| Idempotency + outbox **tables** | Cutover from `partfinder_inventory` |
| Integration tests for posting invariants | WinUI stock mutation pages |

---

## 9. Approvals required before coding live post

1. PO signs `02B-sku-approval-summary.md`.  
2. B1 Mongo txn evidence for target tenant(s).  
3. Engineering sign-off on this spec revision.