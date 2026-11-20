/**
 * Reset WMS / operational tenant data for staging org 900152 (fresh QA).
 * Preserves org_admin_users, org_app_users, and platform organization record.
 *
 * Requires:
 *   MONGODB_URI
 *   WMS_ACCEPTANCE_ALLOW_WRITES=YES_900152
 *
 * Optional:
 *   WMS_STAGING_ORG_CODE (default 900152)
 *   WMS_STAGING_TENANT_DB (default wms_staging_acceptance_900152) — verified against org mapping
 *
 * Usage:
 *   node scripts/staging-reset-tenant-data.mjs           # dry-run (counts only)
 *   node scripts/staging-reset-tenant-data.mjs --execute
 */
import { MongoClient } from 'mongodb';
import { readFileSync, existsSync, writeFileSync, mkdirSync } from 'fs';
import { parse } from 'dotenv';
import { resolve, dirname, join } from 'path';
import { fileURLToPath } from 'url';
import { configureAtlasFriendlyDns } from './mongodb-atlas-dns.mjs';

configureAtlasFriendlyDns();

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
for (const file of ['.env', '.env.local']) {
  const p = resolve(root, file);
  if (existsSync(p)) {
    for (const [k, v] of Object.entries(parse(readFileSync(p)))) {
      if (process.env[k] === undefined) process.env[k] = v;
    }
  }
}

const EXECUTE = process.argv.includes('--execute');
const ORG = (process.env.WMS_STAGING_ORG_CODE ?? '900152').trim();
const EXPECTED_TENANT_DB = (
  process.env.WMS_STAGING_TENANT_DB ?? 'wms_staging_acceptance_900152'
).trim();

const PRESERVE = new Set([
  'org_admin_users',
  'org_app_users',
  '_partfinder_setup',
]);

const EXTRA_WMS_LIKE = ['partfinder_inventory', 'wms_transaction_preflight_probe'];

const WMS_PREFIX = 'wms_';

if (!process.env.MONGODB_URI?.trim()) {
  console.error('MONGODB_URI required');
  process.exit(1);
}

if (EXECUTE && process.env.WMS_ACCEPTANCE_ALLOW_WRITES !== 'YES_900152') {
  console.error(
    'Refusing to delete data without WMS_ACCEPTANCE_ALLOW_WRITES=YES_900152',
  );
  process.exit(2);
}

const platformClient = new MongoClient(process.env.MONGODB_URI, {
  serverSelectionTimeoutMS: 25000,
});

function tenantDbNameFromUri(uri) {
  try {
    const u = new URL(uri);
    const path = decodeURIComponent(u.pathname.replace(/^\//, ''));
    return path.split('?')[0] || '';
  } catch {
    return '';
  }
}

try {
  await platformClient.connect();
  const org = await platformClient
    .db()
    .collection('organizations')
    .findOne({ orgCode: ORG });

  if (!org?.orgDatabaseUri?.trim()) {
    console.error(`Org ${ORG} not found or missing orgDatabaseUri`);
    process.exit(1);
  }

  if (org.wmsEnabled !== true) {
    console.error(`Org ${ORG} is not wmsEnabled — aborting`);
    process.exit(1);
  }

  const tenantUri = org.orgDatabaseUri.trim();
  const tenantDbName = tenantDbNameFromUri(tenantUri);

  if (ORG === '900152' && tenantDbName !== EXPECTED_TENANT_DB) {
    console.error(
      `Refusing reset: org ${ORG} tenant DB is "${tenantDbName}", expected "${EXPECTED_TENANT_DB}"`,
    );
    process.exit(1);
  }

  const tenantClient = new MongoClient(tenantUri, {
    serverSelectionTimeoutMS: 25000,
  });
  await tenantClient.connect();
  const tenantDb = tenantClient.db();

  const allNames = (await tenantDb.listCollections().toArray()).map((c) => c.name);
  const targets = allNames.filter((name) => {
    if (PRESERVE.has(name)) return false;
    if (name.startsWith(WMS_PREFIX)) return true;
    if (EXTRA_WMS_LIKE.includes(name)) return true;
    return false;
  });

  const counts = {};
  for (const name of targets) {
    counts[name] = await tenantDb.collection(name).countDocuments();
  }

  const preservedUsers = {
    adminUsers: await tenantDb.collection('org_admin_users').countDocuments(),
    appUsers: await tenantDb.collection('org_app_users').countDocuments(),
  };

  const report = {
    executedAt: new Date().toISOString(),
    mode: EXECUTE ? 'execute' : 'dry-run',
    orgCode: ORG,
    tenantDatabase: tenantDb.databaseName,
    collectionsToClear: counts,
    preservedUsers,
    preservedCollections: [...PRESERVE],
  };

  console.log(JSON.stringify(report, null, 2));

  if (!EXECUTE) {
    console.error('\nDry-run only. Re-run with --execute to delete the collections above.');
    await tenantClient.close();
    await platformClient.close();
    process.exit(0);
  }

  for (const name of targets) {
    await tenantDb.collection(name).deleteMany({});
  }

  report.deleted = true;
  const evidenceDir = join(root, '..', 'docs', 'wms-phase03', 'evidence');
  mkdirSync(evidenceDir, { recursive: true });
  writeFileSync(
    join(evidenceDir, '03a2-staging-tenant-reset.json'),
    JSON.stringify(report, null, 2),
  );

  console.error(`\nCleared ${targets.length} collection(s). User accounts preserved.`);

  await tenantClient.close();
  await platformClient.close();
} catch (err) {
  console.error(err);
  process.exit(1);
}