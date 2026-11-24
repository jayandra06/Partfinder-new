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
    const conn = await this.tenant.getConnection(uri);
    const session = await conn.startSession();
    try {
      let ran = false;
      await session.withTransaction(async () => {
        ran = true;
      });
      return {
        supported: ran,
        detail: ran
          ? 'Multi-document transactions appear supported on this tenant.'
          : 'Transaction callback did not run.',
      };
    } catch (e) {
      const msg = e instanceof Error ? e.message : 'Transaction probe failed';
      return { supported: false, detail: msg };
    } finally {
      await session.endSession();
    }
  }
}