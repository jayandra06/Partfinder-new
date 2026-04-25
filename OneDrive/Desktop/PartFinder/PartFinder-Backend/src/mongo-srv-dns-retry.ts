import * as dns from 'node:dns';
import mongoose, { type Connection } from 'mongoose';

export function isLikelySrvDnsFailure(err: unknown): boolean {
  const msg = err instanceof Error ? err.message : String(err);
  return /querySrv|ECONNREFUSED|ENOTFOUND|ESERVFAIL|ETIMEOUT/i.test(msg);
}

/** Avoid concurrent `dns.setServers` across requests (Nest async interleaving). */
let dnsSerializedChain: Promise<void> = Promise.resolve();

function runSerializedDnsOp<T>(fn: () => Promise<T>): Promise<T> {
  const done = dnsSerializedChain.then(fn);
  dnsSerializedChain = done.then(
    () => undefined,
    () => undefined,
  );
  return done;
}

/**
 * Opens a Mongoose connection. For `mongodb+srv://`, if the first attempt fails with a
 * typical SRV/DNS error, retries once while temporarily using Cloudflare + Google DNS.
 */
export async function createMongooseConnectionWithSrvDnsRetry(
  uri: string,
): Promise<Connection> {
  const conn = mongoose.createConnection(uri);
  try {
    await conn.asPromise();
    return conn;
  } catch (e1) {
    await conn.close().catch(() => undefined);
    if (!uri.startsWith('mongodb+srv://') || !isLikelySrvDnsFailure(e1)) {
      throw e1;
    }
    return runSerializedDnsOp(async () => {
      const previous = dns.getServers();
      dns.setServers(['1.1.1.1', '8.8.8.8']);
      let conn2: Connection | undefined;
      try {
        conn2 = mongoose.createConnection(uri);
        await conn2.asPromise();
        return conn2;
      } catch (e2) {
        await conn2?.close().catch(() => undefined);
        throw e2;
      } finally {
        dns.setServers(previous);
      }
    });
  }
}
