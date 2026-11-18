import { getWmsDeploymentTier } from './wms-deployment-tier.util';
import { isWmsLiveStockPostingEnabled } from './wms-live-stock.util';

/**
 * Staging-only writes to WMS balances (02B.1).
 * Ignored on `WMS_DEPLOYMENT_TIER=production` even if the boolean flag is set — prevents accidental prod posts.
 */
export function isWmsStagingStockPostingEnabled(): boolean {
  if (process.env.WMS_STAGING_STOCK_POSTING !== 'true') {
    return false;
  }
  const tier = getWmsDeploymentTier();
  return tier === 'staging' || tier === 'development';
}

export function isWmsStockPostingAllowed(): boolean {
  return (
    isWmsStagingStockPostingEnabled() || isWmsLiveStockPostingEnabled()
  );
}