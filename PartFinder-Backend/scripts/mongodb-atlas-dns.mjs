/**
 * Node on Windows may fail mongodb+srv with querySrv ECONNREFUSED while system nslookup works.
 * Call before MongoClient.connect (no credentials logged).
 */
import dns from 'node:dns';

const ATLAS_SRV_DNS = '8.8.8.8';
const LOOPBACK = new Set(['127.0.0.1', '::1', '0.0.0.0']);

export function configureAtlasFriendlyDns() {
  const rest = dns
    .getServers()
    .filter((server) => !LOOPBACK.has(server) && server !== ATLAS_SRV_DNS);
  dns.setServers([ATLAS_SRV_DNS, ...rest]);
  if (typeof dns.setDefaultResultOrder === 'function') {
    dns.setDefaultResultOrder('ipv4first');
  }
}

export async function diagnoseSrv(hostname) {
  configureAtlasFriendlyDns();
  const srvName = `_mongodb._tcp.${hostname}`;
  const out = { hostname, srvName, nodeResolveSrv: null, error: null };
  try {
    const records = await dns.promises.resolveSrv(srvName);
    out.nodeResolveSrv = records.map((r) => ({
      host: r.name,
      port: r.port,
      priority: r.priority,
    }));
  } catch (e) {
    out.error = e instanceof Error ? `${e.code ?? 'ERR'}: ${e.message}` : String(e);
  }
  return out;
}