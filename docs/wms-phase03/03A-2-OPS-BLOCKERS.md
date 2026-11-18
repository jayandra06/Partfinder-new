# Phase 03A.2 — Staging provisioning blockers (2026-11-18)

## What was attempted

| Action | Result |
|--------|--------|
| Dokploy `application-search` / `project-all` | No PartFinder staging app; **no application servers** registered (`server-all` empty) |
| `PartFinder-Backend/.env` | **Missing** — cannot run NestJS against platform Mongo |
| Docker / `mongod` on PATH | **Not available** on agent workstation |
| WinUI manual 100-filter run | **Not executed** (no interactive session + no staging API on QA machine) |
| HTTP `wms:03a2-acceptance-harness` | **Exit 2** — `WMS_*` secrets not configured |

## Minimum ops checklist to unblock **hosted** acceptance

1. Deploy NestJS to a **non-production** hostname with `WMS_DEPLOYMENT_TIER=staging`, `WMS_STAGING_STOCK_POSTING=true`.
2. Provision **Atlas replica set** or dedicated Mongo RS; set platform org `orgDatabaseUri` (isolated DB).
3. Create org `900200` (or assigned code) with `wmsEnabled: true`.
4. Create admin + Operator A/B; grants per runbook.
5. Point WinUI `LicenseApi.BaseUrl` at staging host.
6. Run `npm run wms:staging-preflight` + `npm run wms:03a2-acceptance-harness` + WinUI checklist.
7. Paste evidence into `03A-2-staging-acceptance-report.md` and sign §11.

## Lab evidence (engineering only)

`docs/wms-phase03/evidence/03a2-lab-acceptance-evidence.json` — in-process MongoMemoryReplSet; **does not replace** hosted staging + WinUI sign-off.