import {
  isWmsStagingStockPostingEnabled,
  isWmsStockPostingAllowed,
} from './wms-staging-stock.util';

describe('WMS stock posting gates', () => {
  const prevStaging = process.env.WMS_STAGING_STOCK_POSTING;
  const prevLive = process.env.WMS_LIVE_STOCK_POSTING;

  afterEach(() => {
    process.env.WMS_STAGING_STOCK_POSTING = prevStaging;
    process.env.WMS_LIVE_STOCK_POSTING = prevLive;
  });

  it('remains disabled by default', () => {
    delete process.env.WMS_STAGING_STOCK_POSTING;
    delete process.env.WMS_LIVE_STOCK_POSTING;
    expect(isWmsStagingStockPostingEnabled()).toBe(false);
    expect(isWmsStockPostingAllowed()).toBe(false);
  });

  it('allows staging-only posting when explicitly enabled on staging tier', () => {
    process.env.WMS_DEPLOYMENT_TIER = 'staging';
    process.env.WMS_STAGING_STOCK_POSTING = 'true';
    delete process.env.WMS_LIVE_STOCK_POSTING;
    expect(isWmsStockPostingAllowed()).toBe(true);
  });

  it('ignores staging flag on production tier', () => {
    process.env.WMS_DEPLOYMENT_TIER = 'production';
    process.env.WMS_STAGING_STOCK_POSTING = 'true';
    expect(isWmsStagingStockPostingEnabled()).toBe(false);
    expect(isWmsStockPostingAllowed()).toBe(false);
  });
});