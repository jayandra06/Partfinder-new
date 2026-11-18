import { ForbiddenException } from '@nestjs/common';
import { WmsStagingGateService } from './services/wms-staging-gate.service';

describe('WmsStagingGateService', () => {
  const ctx = {
    orgCode: 'demo',
    tenantUri: 'mongodb://127.0.0.1/demo',
    email: 'a@demo.com',
  } as never;

  it('blocks receiving mutations without preflight evidence', async () => {
    const prev = process.env.WMS_STAGING_STOCK_POSTING;
    process.env.WMS_STAGING_STOCK_POSTING = 'true';
    try {
      const collections = {
        stagingGateEvidence: jest.fn().mockResolvedValue({
          findOne: jest.fn().mockResolvedValue(null),
        }),
        ensurePhase03AIndexes: jest.fn(),
      };
      const diagnostics = { probeTransactions: jest.fn() };
      const svc = new WmsStagingGateService(
        diagnostics as never,
        collections as never,
      );
      await expect(svc.assertReceivingStockMutationsAllowed(ctx)).rejects.toBeInstanceOf(
        ForbiddenException,
      );
    } finally {
      process.env.WMS_STAGING_STOCK_POSTING = prev;
    }
  });
});