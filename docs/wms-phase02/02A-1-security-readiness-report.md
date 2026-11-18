# Phase 02A.1 — Security hardening & Phase 02B readiness

**Date:** 2026-11-17  
**Scope:** Entitlement policy, tenant binding, operator scope, SKU/ERP docs, txn preflight, tests. **No** stock posting.

---

## 1. Corrections implemented

| # | Area | Change |
|---|------|--------|
| 1 | **WMS subscription** | `wmsEnabled` is **opt-in** (`=== true` only). Schema default **false** for new orgs. Existing pilots: set `wmsEnabled: true` in platform DB. |
| 2 | **Tenant identification** | JWT `orgCode` is **authoritative**; `X-Org-Id` must match when sent. Legacy tokens **without** `orgCode` rejected. `JwtStrategy` forwards `orgCode`. |
| 3 | **Operator scope** | Non-admin without `wms_operator_grants` cannot call WMS APIs. List/update paths use `assertWarehouseAccess` / `assertOwnerAccess`. |
| 4 | **SKU** | PO summary in `02B-sku-approval-summary.md` — **no** balance collections until signed. |
| 5 | **Mongo transactions** | Repeatable preflight in `mongodb-transaction-preflight.md` + existing `/diagnostics/transactions`. |
| 6 | **ERP foundation** | `02B-technical-specification.md` § ERP (outbox, mappings, idempotency) — design only. |
| 7 | **Licence API** | `verifyLicense` and setup `status` expose `wmsEnabled` from explicit entitlement. Platform `PATCH` org can set `wmsEnabled`. |

**Non-WMS behaviour:** PartFinder core, login, templates, master data, legacy Inventory unchanged.

---

## 2. Files modified

### Backend

| File | Change |
|------|--------|
| `src/auth/jwt.strategy.ts` | Pass `orgCode` to `req.user` |
| `src/organizations/schemas/organization.schema.ts` | `wmsEnabled` default `false` |
| `src/organizations/wms-entitlement.util.ts` | **New** — explicit entitlement helper |
| `src/organizations/organizations.service.ts` | Entitlement in `verifyLicense`, `findAll`; `update` accepts `wmsEnabled` |
| `src/organizations/dto/update-organization.dto.ts` | Optional `wmsEnabled` |
| `src/setup/setup.service.ts` | `status` returns `wmsEnabled`, WMS limits |
| `src/wms/guards/wms-session.guard.ts` | JWT org binding, entitlement, empty grants |
| `src/wms/guards/wms-org-resolution.util.ts` | **New** |
| `src/wms/guards/wms-session.guard.spec.ts` | Expanded security tests |
| `src/wms/guards/wms-org-resolution.util.spec.ts` | **New** |
| `src/wms/wms-scope.util.spec.ts` | **New** |
| `src/organizations/wms-entitlement.util.spec.ts` | **New** |

### Documentation (new/updated)

| File |
|------|
| `02A-1-security-readiness-report.md` (this file) |
| `02B-sku-approval-summary.md` |
| `02B-technical-specification.md` |
| `mongodb-transaction-preflight.md` |
| `02A-gate-signoff.md` (updated § Phase 02A.1) |

---

## 3. Automated tests (commands & outcomes)

| Command | Scope | Outcome |
|---------|--------|---------|
| `npm test -- --testPathPatterns=wms\|wms-entitlement` | WMS guard, org resolution, scope, entitlement | **PASS** — 4 suites, **18** tests (2026-11-17) |
| `npm run build` | PartFinder-Backend | **PASS** |
| `dotnet build -p:Platform=x64` PartFinder | WinUI | **PASS** (existing nullable warnings only) |

**Not run in this pass (do not count as passed):**

- Full `npm test` (entire backend suite)  
- `npm run test:e2e`  
- WinUI integration / manual WMS CRUD (prior manual sign-off still valid; re-run after entitlement change for pilot orgs)

---

## 4. Open security issues

| Issue | Severity | Mitigation |
|-------|----------|------------|
| Pilot orgs need `wmsEnabled: true` in Mongo | Ops | Run platform admin update for testers only |
| Desktop nav may show WMS for admin before `wmsEnabled` check | Low | API returns 403; optional future: hide nav from `status.wmsEnabled` |
| Platform JWT (portfolio admin) lacks `orgCode` | OK | Cannot access WMS (by design) |
| Grants admin UI not built | Medium | Admins only until grants CRUD API/UI |

---

## 5. SKU decisions requiring PO approval

See **`02B-sku-approval-summary.md`** checklist (6 items). **Live balances blocked** until signed.

---

## 6. MongoDB transaction readiness

| Item | Status |
|------|--------|
| API probe endpoint | **Implemented** |
| Written preflight procedure | **`mongodb-transaction-preflight.md`** |
| Staging replica evidence on file | **Team** — attach per org in ticket |
| Production tenant preflight | **Required** before 02B go-live per tenant |

---

## 7. ERP integration foundation (02B design)

Documented in **`02B-technical-specification.md`**: connectors, SKU mappings, order/doc refs, location maps, inbound idempotency, transactional outbox, sync status, retry/reconciliation. **No** SAP/Oracle implementation in 02A.1.

---

## 8. Is Phase 02B stock posting safe to begin?

| Gate | Ready? |
|------|--------|
| 02A.1 code merged | Yes (this delivery) |
| Automated security unit tests green | Verify §3 |
| PO signs `02B-sku-approval-summary.md` | **No** — pending |
| B1 evidence per staging tenant | **Ops record** required |
| Pilot `wmsEnabled: true` | **Ops** for test orgs only |

**Conclusion:** **Safe to start 02B implementation** (schema, posting service behind feature flag, dry-run only) **after** PO SKU approval and B1 staging evidence are recorded. **Not safe** to enable **live** stock posting or production cutover until those gates close.

---

## Pilot transition (WMS testing orgs)

1. In platform DB or admin API: `PATCH /organizations/:id` with `{ "wmsEnabled": true }`.  
2. Ensure users sign in via **desktop invite/refresh session** (JWT must include `orgCode`).  
3. Re-run manual smoke: WMS Setup + transaction probe.  
4. All other orgs remain `wmsEnabled: false` (default).