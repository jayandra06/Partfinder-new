# Phase 03A.2 — Staging deployment package (no secrets)

**Purpose:** Safe, repeatable staging host for WMS receiving acceptance.  
**Out of scope:** Production live posting, Phase 03B, ERP live connectors.

---

## 1. Architecture

```text
┌─────────────────┐     HTTPS JWT      ┌──────────────────────────┐
│ PartFinder WinUI │ ────────────────► │ NestJS staging API        │
│ (staging URL)   │                    │ WMS_DEPLOYMENT_TIER=staging │
└─────────────────┘                    └───────────┬──────────────┘
                                                   │
                     ┌─────────────────────────────┼─────────────────────────────┐
                     │ Platform Mongo (orgs)       │ Tenant Mongo (per org URI)   │
                     │ wmsEnabled, orgDatabaseUri  │ wms_* collections, txn RS    │
                     └─────────────────────────────┴─────────────────────────────┘
```

| Layer | Isolation rule |
|-------|----------------|
| API hostname | **Different** from production (`backend.partfinder.store`) |
| Platform DB | Staging org uses **dedicated** `orgDatabaseUri` — no production tenant data |
| Posting flags | Staging tier + explicit boolean; **not** combinable on production tier |

---

## 2. NestJS staging environment (template)

Copy from `PartFinder-Backend/.env.example`. Set on the **staging host only**:

| Variable | Staging value | Production value (reference) |
|----------|---------------|------------------------------|
| `MONGODB_URI` | Platform DB connection (secret) | Production platform URI |
| `JWT_SECRET` | Staging secret (≠ prod) | Production secret |
| `WMS_DEPLOYMENT_TIER` | **`staging`** | **`production`** |
| `WMS_STAGING_STOCK_POSTING` | `true` | `false` or unset |
| `WMS_LIVE_STOCK_POSTING` | `false` or unset | `false` until PO-approved live cutover |
| `PORT` / `CORS_ORIGIN` | Staging bind + WinUI origins | Production |

### Posting safety (mandatory)

Staging stock mutation requires **both**:

1. `WMS_DEPLOYMENT_TIER=staging` (or `development` for local lab only)
2. `WMS_STAGING_STOCK_POSTING=true`

On **`WMS_DEPLOYMENT_TIER=production`**, `WMS_STAGING_STOCK_POSTING=true` is **ignored** — flipping one env var on prod cannot enable staging posts.

Live posting (future) requires **`WMS_DEPLOYMENT_TIER=production`** **and** `WMS_LIVE_STOCK_POSTING=true`.

Verify after deploy:

```http
GET /api/wms/v1/health
Authorization: Bearer <admin JWT>
```

Expect: `stagingStockPosting: true`, `liveStockPosting: false`, tier reflected in preflight evidence `deploymentTier`.

---

## 3. Test organization (platform record)

Create via admin tooling or controlled seed (values are **examples** — use your assigned org code):

| Field | Example | Notes |
|-------|---------|-------|
| `orgCode` | `900150` | 6-digit staging-only code |
| `name` | `WMS Staging Acceptance` | |
| `wmsEnabled` | `true` | Required for WMS routes |
| `orgDatabaseUri` | *from vault* | Replica-set MongoDB; **isolated** database name |
| `maxWmsWarehouses` | `10` | |
| `maxStockOwners` | `50` | |
| Validity / license | Active | `OrganizationsService.verifyLicense` must pass |

**Users (tenant DB):**

| Persona | Email | Grants |
|---------|-------|--------|
| WMS admin | `wms-admin@staging.test` | Org admin → full WMS |
| Operator A | `operator-a@staging.test` | WH demo + **Client A** only; receiving/QC/put-away |
| Operator B | `operator-b@staging.test` | **Client B** only; negative auth test |

---

## 4. WinUI API configuration

Default in repo: `PartFinder/PartFinder/appsettings.json` → `LicenseApi.BaseUrl` points at production.

**Staging QA workstations** must override (do not commit secrets):

| Method | Instruction |
|--------|-------------|
| **Per-machine** | Edit deployed `appsettings.json` beside `PartFinder.exe` → `"BaseUrl": "https://<staging-api-host>"` |
| **Build flavor** (future) | Separate `appsettings.Staging.json` copied at release time |

Sign-in uses normal org code + credentials; all WMS calls go to `https://<staging-api-host>/api/wms/v1/...`.

---

## 5. Optional local staging-lab (developers)

Not a substitute for named staging acceptance.

```powershell
cd PartFinder-Backend
docker compose -f docker/wms-staging-mongo-compose.yml up -d
```

Local API `.env`:

```env
WMS_DEPLOYMENT_TIER=development
WMS_STAGING_STOCK_POSTING=true
```

Point one pilot org `orgDatabaseUri` at the lab replica set (see runbook).

---

## 6. Deployment checklist (ops)

- [ ] Staging hostname DNS + TLS
- [ ] `WMS_DEPLOYMENT_TIER=staging` on staging host
- [ ] Production host confirmed `WMS_DEPLOYMENT_TIER=production`
- [ ] Staging org `wmsEnabled` + isolated tenant URI
- [ ] WinUI QA machines pointed at staging `BaseUrl`
- [ ] Preflight recorded (`POST /wms/v1/staging-gate/preflight`)
- [ ] Operator grants for A/B personas
- [ ] Run automated gate script (§7)

---

## 7. Automated gate (engineering)

```powershell
cd PartFinder-Backend
.\scripts\run-03a2-automated-gate.ps1
```

Then manual WinUI: [`03A-2-winui-acceptance-checklist.md`](./03A-2-winui-acceptance-checklist.md).

---

## 8. Evidence handoff

| Evidence | Where to file |
|----------|----------------|
| Preflight JSON (redacted) | `03A-2-staging-acceptance-report.md` §2 |
| WinUI checklist rows | Same §3 |
| Balance reconciliation | §4 |
| Engineering sign-off | §7 / §9 |

**Do not** mark **ACCEPTED** until all mandatory rows pass on the **named staging tenant**.

---

*Related:* [`03A-2-staging-runbook.md`](./03A-2-staging-runbook.md), [`03A-2-staging-acceptance-report.md`](./03A-2-staging-acceptance-report.md)