import { MongoClient } from 'mongodb';
import { configureAtlasFriendlyDns } from './mongodb-atlas-dns.mjs';

configureAtlasFriendlyDns();
import { readFileSync, existsSync } from 'fs';
import { parse } from 'dotenv';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const envPath = resolve(root, '.env');
if (existsSync(envPath)) {
  Object.assign(process.env, parse(readFileSync(envPath)));
}

const uri = process.env.MONGODB_URI;
if (!uri) {
  console.error('MONGODB_URI missing');
  process.exit(2);
}

const client = new MongoClient(uri);
await client.connect();
try {
  const db = client.db();
  const coll = db.collection('organizations');
  const rows = await coll
    .find({ orgDatabaseUri: { $exists: true, $type: 'string' } })
    .project({
      orgCode: 1,
      name: 1,
      wmsEnabled: 1,
      status: 1,
      plan: 1,
      orgDatabaseUri: 1,
    })
    .limit(40)
    .toArray();
  for (const o of rows) {
    const uriSet = Boolean(String(o.orgDatabaseUri ?? '').trim());
    console.log(
      `${o.orgCode}  wms=${o.wmsEnabled === true}  tenantUri=${uriSet}  ${o.name ?? ''}  [${o.status}]`,
    );
  }
} finally {
  await client.close();
}