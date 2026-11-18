import dns from 'node:dns';

/** Public resolver that answers Atlas `_mongodb._tcp` SRV lookups. */
export const ATLAS_SRV_DNS = '8.8.8.8';

const LOOPBACK = new Set(['127.0.0.1', '::1', '0.0.0.0']);

/**
 * `mongodb+srv://` uses Node's c-ares resolver. On this host that list is only
 * `127.0.0.1`, which refuses SRV (`querySrv ECONNREFUSED`). Put Google DNS first
 * and drop loopback so the lookup does not stop there.
 */
export function configureAtlasFriendlyDns(): void {
  const rest = dns
    .getServers()
    .filter((server) => !LOOPBACK.has(server) && server !== ATLAS_SRV_DNS);
  dns.setServers([ATLAS_SRV_DNS, ...rest]);
  if (typeof dns.setDefaultResultOrder === 'function') {
    dns.setDefaultResultOrder('ipv4first');
  }
}

configureAtlasFriendlyDns();