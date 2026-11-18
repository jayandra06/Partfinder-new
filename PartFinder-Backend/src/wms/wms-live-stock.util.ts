import { getWmsDeploymentTier } from './wms-deployment-tier.util';

/**
 * Live balance/ledger posts are disabled until ops explicitly enables them on production tier only.
 * Phase 02B implementation uses dry-run only by default.
 */
export function isWmsLiveStockPostingEnabled(): boolean {
  if (process.env.WMS_LIVE_STOCK_POSTING !== 'true') {
    return false;
  }
  return getWmsDeploymentTier() === 'production';
}