import { Injectable, ServiceUnavailableException } from '@nestjs/common';
import { WmsDiagnosticsService } from './wms-diagnostics.service';

@Injectable()
export class WmsTransactionGuardService {
  private cache = new Map<string, { supported: boolean; checkedAt: number }>();
  private readonly ttlMs = 5 * 60 * 1000;

  constructor(private readonly diagnostics: WmsDiagnosticsService) {}

  async assertTransactionsSupported(tenantUri: string): Promise<void> {
    const cached = this.cache.get(tenantUri);
    const now = Date.now();
    if (cached && now - cached.checkedAt < this.ttlMs) {
      if (!cached.supported) {
        throw new ServiceUnavailableException(
          'MongoDB multi-document transactions are not available for this tenant.',
        );
      }
      return;
    }
    const probe = await this.diagnostics.probeTransactions(tenantUri);
    this.cache.set(tenantUri, { supported: probe.supported, checkedAt: now });
    if (!probe.supported) {
      throw new ServiceUnavailableException(
        probe.detail || 'MongoDB transactions unavailable.',
      );
    }
  }
}