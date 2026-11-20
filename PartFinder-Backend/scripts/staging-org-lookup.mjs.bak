import { MongoClient } from 'mongodb';
import { readFileSync, existsSync } from 'fs';
import { parse } from 'dotenv';
import { resolve, dirname } from 'path';
import { fileURLToPath } from 'url';
import { configureAtlasFriendlyDns } from './mongodb-atlas-dns.mjs';

configureAtlasFriendlyDns();

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
if (existsSync(resolve(root, '.env'))) {
  Object.assign(process.env, parse(readFileSync(resolve(root, '.env'))));
}

const code = (process.argv[2] ?? '900152').trim();
const client = new MongoClient(process.env.MONGODB_URI);
await client.connect();
try {
  const doc = await client.db().collection('organizations').findOne({ orgCode: code });
  if (!doc) {
    console.log(`org ${code}: NOT FOUND on platform`);
    process.exit(0);
  }
  const uri = String(doc.orgDatabaseUri ?? '').trim();
  let dbName = null;
  if (uri) {
    try {
      const pseudo = uri.replace('mongodb+srv://', 'https://').replace('mongodb://', 'http://');
      dbName = new URL(pseudo).pathname.replace(/^\//, '') || null;
    } catch {
      dbName = '<parse-failed>';
    }
  }
  console.log(
    JSON.stringify(
      {
        orgCode: doc.orgCode,
        name: doc.name,
        wmsEnabled: doc.wmsEnabled === true,
        status: doc.status,
        plan: doc.plan,
        hasTenantUri: Boolean(uri),
        tenantDatabaseName: dbName,
        platformHostOnly: uri ? 'atlas-cluster-redacted' : null,
      },
      null,
      2,
    ),
  );
} finally {
  await client.close();
}