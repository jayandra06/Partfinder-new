# Phase 03A.2 — MongoDB connectivity diagnosis

**Date:** 2026-11-18  
**Issue:** `querySrv ECONNREFUSED` when Node.js connects with `mongodb+srv://`

---

## Symptom

```
Error: querySrv ECONNREFUSED _mongodb._tcp.part-finder-admindb.semhy39.mongodb.net
```

Windows `nslookup -type=SRV` succeeded via LAN resolver `192.168.1.1`; Node.js failed.

---

## Root cause

| Check | Result |
|-------|--------|
| Node `dns.getServers()` before fix | **`127.0.0.1` only** |
| SRV query to localhost DNS | **Refused** (`ECONNREFUSED`) |
| System nslookup (192.168.1.1) | **Pass** — 3 shard targets returned |

Node’s built-in DNS resolver was not using the working LAN/public resolvers that Windows uses for `nslookup`.

---

## Fix (repository)

1. `PartFinder-Backend/scripts/mongodb-atlas-dns.mjs` — merge `192.168.1.1`, `1.1.1.1`, `8.8.8.8` into `dns.setServers()`; `ipv4first` order.  
2. `PartFinder-Backend/src/test-utils/mongodb-atlas-dns.ts` — same for Jest/TS.  
3. Wired into: `list-wms-staging-orgs.mjs`, `wms-03a2-staging-acceptance.spec.ts`, `wms-03a2-provision-and-acceptance.ts`.  
4. Verify: `npm run wms:mongodb-dns-preflight` → **PASS**.

**Not required:** opening Atlas to `0.0.0.0/0` (avoid unless Atlas Network Access already blocks this host).

---

## Atlas Network Access

If connectivity fails after DNS fix, confirm the client egress IP is allow-listed in Atlas **Network Access** (ops console). This document does not record IP lists. **Do not** use `0.0.0.0/0` as the default fix for SRV errors — resolver fix comes first.

---

## Runtime matrix (where connectivity applies)

| Runtime | NestJS? | Typical failure seen | Notes |
|---------|---------|----------------------|--------|
| **Vercel** (`Partfinder-Portfolio`) | **No** — static SPA + rewrite to `backend.partfinder.store` | N/A for direct Atlas from Vercel | Website ≠ API |
| **Production API** (`backend.partfinder.store`) | **Yes** (inferred) | Unknown from repo — needs `config:diagnostics` on server | Posting flags not in git |
| **Staging API** | **Not in repo** | — | WinUI must not assume staging on prod URL |
| **Local Jest acceptance** | In-process services | **`querySrv ECONNREFUSED`** before DNS fix | Uses `.env` `MONGODB_URI`; fixed via `mongodb-atlas-dns` |
| **Dokploy** | **No catalog match** | — | MCP search `partfinder` returned 0 apps (2026-11-18) |

---

*See acceptance report — **Database connectivity fix — Tasks 1–4**.*