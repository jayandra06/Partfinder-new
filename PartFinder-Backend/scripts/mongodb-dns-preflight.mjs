import dns from 'node:dns';
import { configureAtlasFriendlyDns, diagnoseSrv } from './mongodb-atlas-dns.mjs';

const CLUSTER_HOST = 'part-finder-admindb.semhy39.mongodb.net';

console.log('MongoDB Atlas DNS preflight (no credentials)');
console.log('  nodeDnsServers(before):', dns.getServers());

configureAtlasFriendlyDns();
console.log('  nodeDnsServers(after):', dns.getServers());

const diag = await diagnoseSrv(CLUSTER_HOST);
console.log('  resolveSrv:', JSON.stringify(diag, null, 2));

if (diag.error) {
  console.log('\nRESULT: FAIL — fix Node DNS or use standard mongodb:// seed list from nslookup');
  process.exit(1);
}
console.log('\nRESULT: PASS — Node can resolve Atlas SRV records');