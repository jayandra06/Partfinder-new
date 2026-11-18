# Phase 02B — Acceptance test plan

**Runs in:** Same phase that delivers each capability (per roadmap).  
**Prerequisite:** PO SKU approval before any test that creates production balances.

---

## Automated (target in 02B implementation)

| ID | Test | Pass criteria |
|----|------|---------------|
| T-SKU-01 | Register `skuId`, add MPN identifier | Second same MPN+different mfr creates distinct SKU |
| T-SKU-02 | Duplicate `masterDataRow` link | Reject second active link for same template+row |
| T-SKU-03 | Ambiguity report | Two master rows same heuristics → report, no auto-merge |
| T-LDG-01 | Post `receive` increases onHand | Txn row exists; balance version++ |
| T-LDG-02 | Post `move` | Total onHand unchanged; locations updated |
| T-LDG-03 | Idempotency replay | Same key → no double post |
| T-LDG-04 | Reversal | Reversal txn; net qty correct |
| T-LDG-05 | Owner transfer | Owner A −, owner B +; no other owner touched |
| T-LDG-06 | Concurrent post | Optimistic lock or txn serializes |
| T-ATP-01 | ATP formula | blocked + reserved reduce available only |
| T-SEC-01 | Cross-owner API | Employee grant owner A cannot read owner B balance |
| T-ERP-01 | Inbound duplicate idempotency | Second message same key → no duplicate effect |

**Security regression:** Re-run `npm test -- --testPathPatterns=wms|wms-entitlement` (02A.1 suite).

---

## Manual / staging (record evidence)

| ID | Test | Evidence |
|----|------|----------|
| M-B1 | `GET /api/wms/v1/diagnostics/transactions` | JSON in ticket (`mongodb-transaction-preflight.md`) |
| M-B1b | `mongosh` withTransaction script | Output attached per tenant |
| M-DRY-01 | Opening balance dry-run on test org | Report: mapped / unmapped / ambiguous counts |
| M-ENT-01 | Org without `wmsEnabled` | WMS API 403 |
| M-ENT-02 | Pilot org `wmsEnabled: true` | Setup + registry APIs 200 |
| M-LEG-01 | Legacy Inventory edit | `partfinder_inventory` still works |

---

## Roadmap AT mapping (02B portion)

| AT | Coverage in 02B |
|----|-----------------|
| AT-2 | Owner-separated balances (T-LDG-05, T-SEC-01) |
| AT-13 | Stock math partial (move, adjust — no pick/pack) |
| AT-17 | Ledger immutability + reversal (T-LDG-04) |

Full AT-13 pick/pack/dispatch portions deferred to Phase 03–04.

---

## Not executed in preparation phase

| Item | Status |
|------|--------|
| T-LDG-* integration tests | **Not built** — spec only |
| M-B1 on named production tenant | **Ops** — attach when run |
| E2E WinUI ledger UI | **N/A** — read APIs later |
| Full `npm run test:e2e` | **Not run** in 02B prep task |