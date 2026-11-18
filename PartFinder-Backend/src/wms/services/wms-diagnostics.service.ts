import { Injectable } from '@nestjs/common';
import { TenantMongoService } from '../../setup/tenant-mongo.service';

@Injectable()
export class WmsDiagnosticsService {
  constructor(private readonly tenant: TenantMongoService) {}

  async pingTenant(uri: string): Promise<{ ok: boolean }> {
    await this.tenant.pingUri(uri);
    return { ok: true };
  }

  async probeTransactions(uri: string): Promise<{
    supported: boolean;
    detail: string;
  }> {
    const r = await this.probeTransactionReadiness(uri);
    return {
      supported: r.supported,
      detail: r.summary,
    };
  }

  /** Commit + rollback probes on the authenticated tenant connection (no secrets). */
  async probeTransactionReadiness(uri: string): Promise<{
    supported: boolean;
    summary: string;
    databaseName: string | null;
    replicaSetName: string | null;
    commitProbe: boolean;
    rollbackProbe: boolean;
  }> {
    const conn = await this.tenant.getConnection(uri);
    const db = conn.db;
    if (!db) {
      return {
        supported: false,
        summary: 'Tenant connection has no database.',
        databaseName: null,
        replicaSetName: null,
        commitProbe: false,
        rollbackProbe: false,
      };
    }

    let replicaSetName: string | null = null;
    try {
      const hello = await db.admin().command({ hello: 1 });
      if (hello && typeof hello === 'object' && 'setName' in hello) {
        replicaSetName = hello.setName ? String(hello.setName) : null;
      }
    } catch {
      replicaSetName = null;
    }

    const probeColl = db.collection('wms_transaction_preflight_probe');
    const marker = `probe-${Date.now()}`;

    let commitProbe = false;
    const commitSession = await conn.startSession();
    try {
      await commitSession.withTransaction(async () => {
        await probeColl.insertOne({ marker, phase: 'commit', at: new Date() }, { session: commitSession });
        commitProbe = true;
      });
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'commit probe failed';
      return {
        supported: false,
        summary: msg,
        databaseName: db.databaseName,
        replicaSetName,
        commitProbe: false,
        rollbackProbe: false,
      };
    } finally {
      await commitSession.endSession();
    }

    let rollbackProbe = false;
    const rollbackSession = await conn.startSession();
    const rollbackMarker = `${marker}-rb`;
    try {
      try {
        await rollbackSession.withTransaction(async () => {
          await probeColl.insertOne(
            { marker: rollbackMarker, phase: 'rollback', at: new Date() },
            { session: rollbackSession },
          );
          throw new Error('ROLLBACK_PROBE');
        });
      } catch (e) {
        if (e instanceof Error && e.message === 'ROLLBACK_PROBE') {
          const leaked = await probeColl.findOne({ marker: rollbackMarker });
          rollbackProbe = leaked === null;
        }
      }
    } finally {
      await rollbackSession.endSession();
      await probeColl.deleteMany({ marker: { $in: [marker, rollbackMarker] } });
    }

    const supported = commitProbe && rollbackProbe;
    const summary = supported
      ? 'Commit and rollback probes succeeded on tenant database.'
      : `Incomplete: commit=${commitProbe}, rollback=${rollbackProbe}`;

    return {
      supported,
      summary,
      databaseName: db.databaseName,
      replicaSetName,
      commitProbe,
      rollbackProbe,
    };
  }
}