# Phase 03A.2 — WinUI acceptance checklist (100 filters)

**Environment:** Named staging API + staging org only.  
**Personas:** Operator A (Client A), Operator B (Client B), WMS admin for preflight.  
**Est. time:** 45–60 minutes with seeded master data.

---

## Pre-flight (admin)

| # | Step | Expected | Pass |
|---|------|----------|------|
| P1 | Confirm WinUI `LicenseApi.BaseUrl` is **staging** host | Not `backend.partfinder.store` for this run | ☐ |
| P2 | Sign in as WMS admin | Workbook visible | ☐ |
| P3 | Administration → **Record preflight** | Gate summary: `supported=true`, commit/rollback true, `stagingPost=true` | ☐ |
| P4 | Export or copy preflight evidence to acceptance report §2 | No secrets in git | ☐ |

---

## Master data (admin — once per tenant)

| # | Step | Expected | Pass |
|---|------|----------|------|
| M1 | Create warehouse e.g. `WH-DEMO-01` | Appears in warehouse list | ☐ |
| M2 | Locations: `RECV-01` (receiving), `A-01-01` (storage) | Selectable in receiving/put-away | ☐ |
| M3 | Stock owners **Client A**, **Client B** | Both exist | ☐ |
| M4 | SKU **Filter**, UOM **EA** | Registry row active | ☐ |
| M5 | Operator grants: A → WH + Client A; B → Client B only | Saved in Administration | ☐ |

---

## Operator scope verification (A then B)

| # | Step | Expected | Pass |
|---|------|----------|------|
| S1 | Sign in as **Operator A** → Workbook | Loads without admin grants UI write | ☐ |
| S2 | Receiving owner combo / grant scope | **Client A** only (no Client B in dropdowns) | ☐ |
| S3 | Warehouse selector | Only granted warehouse(s) | ☐ |
| S4 | Sign in as **Operator B** | Client B only in owner lists | ☐ |

*API contract:* `GET /wms/v1/warehouses` and `GET /wms/v1/stock-owners` filter by operator grants (verified in `wms-setup-operator-scope.spec.ts`).

---

## Scenario: 100 filters (Operator A)

| # | Step | Data | Expected | Pass |
|---|------|------|----------|------|
| 1 | Expected receipt | PO optional; owner A; line 100 × Filter | Status `open` | ☐ |
| 2 | Save expected lines | Qty 100 | Persisted | ☐ |
| 3 | Goods receipt | Qty **60**, idempotency key **recorded** (e.g. clipboard) | Posted; expected `partially_received` | ☐ |
| 4 | QC partial | Accept **55**, reject **5** | Reject remains on-hand **blocked** at receiving | ☐ |
| 5 | Put-away | **55** → `A-01-01` | Task complete | ☐ |
| 6 | Balances | WH-level / location drill | Total on-hand **60**; available **55**; blocked **5** | ☐ |
| 7 | Expected remaining | Same expected doc | **40** remain on line | ☐ |
| 8 | Movement ledger | Filter by SKU | receive, qcBlock, qcRelease, move entries | ☐ |
| 9 | Idempotency | Repeat confirm with **same** key | `duplicate`/already recorded; on-hand still **60** | ☐ |
| 10 | Operator B | Attempt create receipt for **owner A** | **403** + clear message | ☐ |
| 11 | ERP outbox | Admin/diagnostics view if exposed | Pending only; no live ERP | ☐ |

---

## Evidence to attach

- Screenshot: Administration gate summary after P3  
- Screenshot: Balances after step 6  
- Exported XLSX: expected receipts, goods receipts, or receipt lines (Export button)  
- Screenshot or log: step 10 forbidden  
- Optional: `GET staging-gate/latest` JSON (redacted)

---

## Failures

Log in acceptance report §3 with:

- Step #
- Actual vs expected
- API error text (from status bar)
- Ticket link

**Do not** mark phase accepted until failures are resolved or explicitly waived by engineering lead with reason.

---

*Automated math reference:* `wms-receiving-workflow.integration.spec.ts` (replSet — not staging evidence).