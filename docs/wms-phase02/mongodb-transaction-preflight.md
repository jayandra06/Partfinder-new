# MongoDB transaction preflight (B1)

**Rule:** No stock posting without **multi-document transaction** support on the **tenant** database (D08). **No** compensating fallback.

---

## Staging evidence (repeatable)

### A. Application probe (02A)

```http
GET /api/wms/v1/diagnostics/transactions
Authorization: Bearer <org-scoped token>
X-Org-Id: <same org as JWT>
```

Record JSON: `{ "supported": true|false, "detail": "..." }`.

### B. `mongosh` (operations)

```javascript
// Replace with tenant connection string (from platform org record only — never commit secrets)
const uri = 'mongodb+srv://...';
const client = new MongoClient(uri);
await client.connect();
const admin = client.db('admin');
const hello = await admin.command({ hello: 1 });
// Replica set: hello.setName should be defined for transaction support on sharded/replica deployments
const session = client.startSession();
await session.withTransaction(async () => {
  const db = client.db(); // tenant DB name from URI
  const c = db.collection('wms_transaction_probe');
  await c.insertOne({ probe: true, at: new Date() }, { session });
});
await session.endSession();
```

**Pass:** `withTransaction` completes without error.  
**Fail:** Block Phase 02B posting; remediate topology (replica set / Atlas tier).

---

## Production preflight (per tenant, before stock-changing features)

| Step | Owner | Record |
|------|-------|--------|
| 1 | Ops | Tenant URI host, cluster type (Atlas M10+, replica set name) |
| 2 | Eng | API probe result + date + org code |
| 3 | Eng | Optional `mongosh` script output attached to change ticket |
| 4 | PO | Approval to enable ledger for this tenant |

Store evidence in ticket or `docs/wms-phase02/evidence/<orgCode>-txn-<date>.md` (gitignored if contains hostnames only).

---

## Topology notes

| Deployment | Transactions |
|------------|----------------|
| MongoDB Atlas replica set | Usually **supported** |
| Single-node dev `mongod` | Often **not** supported — OK for 02A setup CRUD only |
| Serverless / M0 limitations | Verify Atlas docs for txn support |

**Unsafe patterns (forbidden):** posting outside transactions; retry without idempotency; dual-write to `partfinder_inventory` and WMS ledger.