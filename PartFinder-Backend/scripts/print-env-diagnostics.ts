/**
 * Safe env diagnostics — prints presence only, never secret values.
 * Run: npm run config:diagnostics --prefix PartFinder-Backend
 */
import { existsSync, readFileSync } from 'fs';
import { parse } from 'dotenv';
import { backendEnvFilePaths, backendRootDir } from '../src/config/backend-env';
import {
  isLicensePurchaseEnabled,
  isRazorpayConfigured,
  isRazorpayTestKeyId,
  razorpayConfigurationMessage,
  readRazorpayKeys,
} from '../src/license-purchase/license-purchase.config';
import { ConfigService } from '@nestjs/config';
import { getWmsDeploymentTier } from '../src/wms/wms-deployment-tier.util';
import {
  isWmsStagingStockPostingEnabled,
  isWmsStockPostingAllowed,
} from '../src/wms/wms-staging-stock.util';
import { isWmsLiveStockPostingEnabled } from '../src/wms/wms-live-stock.util';

const envFiles = backendEnvFilePaths();
for (const file of envFiles) {
  if (existsSync(file)) {
    Object.assign(process.env, parse(readFileSync(file)));
  }
}

const config = new ConfigService(process.env);
const { keyId } = readRazorpayKeys(config);

console.log('PartFinder-Backend environment diagnostics');
console.log('  backendRoot:', backendRootDir());
console.log('  envFiles:', envFiles.join(', '));
console.log('  MONGODB_URI:', process.env.MONGODB_URI ? 'set' : 'MISSING');
console.log('  JWT_SECRET:', process.env.JWT_SECRET ? 'set' : 'MISSING');
console.log('  LICENSE_PURCHASE_ENABLED:', String(isLicensePurchaseEnabled(config)));
console.log('  RAZORPAY_KEY_ID:', keyId ? `set (${isRazorpayTestKeyId(keyId) ? 'test' : 'live'} prefix)` : 'MISSING');
console.log('  RAZORPAY_KEY_SECRET:', readRazorpayKeys(config).keySecret ? 'set' : 'MISSING');
console.log('  razorpayReady:', isRazorpayConfigured(config) && isLicensePurchaseEnabled(config));
const message = razorpayConfigurationMessage(config);
if (message) {
  console.log('  note:', message);
}

console.log('  WMS_DEPLOYMENT_TIER:', getWmsDeploymentTier());
console.log(
  '  WMS_STAGING_STOCK_POSTING (raw):',
  process.env.WMS_STAGING_STOCK_POSTING === 'true' ? 'true' : 'false/unset',
);
console.log(
  '  WMS_LIVE_STOCK_POSTING (raw):',
  process.env.WMS_LIVE_STOCK_POSTING === 'true' ? 'true' : 'false/unset',
);
console.log('  wmsStagingPostingEffective:', isWmsStagingStockPostingEnabled());
console.log('  wmsLivePostingEffective:', isWmsLiveStockPostingEnabled());
console.log('  wmsStockPostingAllowed:', isWmsStockPostingAllowed());
