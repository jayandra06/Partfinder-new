import { getWmsDeploymentTier } from './wms-deployment-tier.util';
import { isWmsStagingStockPostingEnabled } from './wms-staging-stock.util';
import { isWmsLiveStockPostingEnabled } from './wms-live-stock.util';

describe('WMS deployment tier', () => {
  const prevTier = process.env.WMS_DEPLOYMENT_TIER;
  const prevStaging = process.env.WMS_STAGING_STOCK_POSTING;
  const prevLive = process.env.WMS_LIVE_STOCK_POSTING;

  afterEach(() => {
    process.env.WMS_DEPLOYMENT_TIER = prevTier;
    process.env.WMS_STAGING_STOCK_POSTING = prevStaging;
    process.env.WMS_LIVE_STOCK_POSTING = prevLive;
  });

  it('defaults to development when NODE_ENV is not production', () => {
    delete process.env.WMS_DEPLOYMENT_TIER;
    delete process.env.NODE_ENV;
    expect(getWmsDeploymentTier()).toBe('development');
  });

  it('defaults to production when NODE_ENV is production and tier unset', () => {
    delete process.env.WMS_DEPLOYMENT_TIER;
    process.env.NODE_ENV = 'production';
    expect(getWmsDeploymentTier()).toBe('production');
  });

  it('blocks staging posting on production tier even when flag is true', () => {
    process.env.WMS_DEPLOYMENT_TIER = 'production';
    process.env.WMS_STAGING_STOCK_POSTING = 'true';
    expect(isWmsStagingStockPostingEnabled()).toBe(false);
  });

  it('allows staging posting only on staging or development tier', () => {
    process.env.WMS_STAGING_STOCK_POSTING = 'true';
    process.env.WMS_DEPLOYMENT_TIER = 'staging';
    expect(isWmsStagingStockPostingEnabled()).toBe(true);
    process.env.WMS_DEPLOYMENT_TIER = 'production';
    expect(isWmsStagingStockPostingEnabled()).toBe(false);
  });

  it('allows live posting only on production tier', () => {
    process.env.WMS_LIVE_STOCK_POSTING = 'true';
    process.env.WMS_DEPLOYMENT_TIER = 'staging';
    expect(isWmsLiveStockPostingEnabled()).toBe(false);
    process.env.WMS_DEPLOYMENT_TIER = 'production';
    expect(isWmsLiveStockPostingEnabled()).toBe(true);
  });
});