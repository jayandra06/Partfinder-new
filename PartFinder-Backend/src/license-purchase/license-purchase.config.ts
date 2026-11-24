import { ConfigService } from '@nestjs/config';

export function isLicensePurchaseEnabled(config: ConfigService): boolean {
  const raw = config.get<string>('LICENSE_PURCHASE_ENABLED');
  if (raw === undefined || raw === null || String(raw).trim() === '') {
    return true;
  }
  const normalized = String(raw).trim().toLowerCase();
  return normalized === '1' || normalized === 'true' || normalized === 'yes';
}

const PLACEHOLDER_KEY_IDS = new Set([
  'rzp_test_your_key_id_here',
  'rzp_test_xxxxxxxx',
  'your_key_id',
]);

const PLACEHOLDER_SECRETS = new Set([
  'your_test_key_secret_here',
  'your_key_secret',
  'placeholder_secret',
]);

function isPlaceholderKeyId(keyId: string): boolean {
  const v = keyId.trim().toLowerCase();
  return !v || PLACEHOLDER_KEY_IDS.has(v) || v.includes('your_key_id');
}

function isPlaceholderSecret(keySecret: string): boolean {
  const v = keySecret.trim().toLowerCase();
  return !v || PLACEHOLDER_SECRETS.has(v) || v.includes('your_test_key_secret');
}

export function readRazorpayKeys(config: ConfigService): {
  keyId: string;
  keySecret: string;
} {
  const keyId = config.get<string>('RAZORPAY_KEY_ID')?.trim() ?? '';
  const keySecret = config.get<string>('RAZORPAY_KEY_SECRET')?.trim() ?? '';
  return { keyId, keySecret };
}

export function isRazorpayConfigured(config: ConfigService): boolean {
  const { keyId, keySecret } = readRazorpayKeys(config);
  if (isPlaceholderKeyId(keyId) || isPlaceholderSecret(keySecret)) {
    return false;
  }
  return Boolean(keyId && keySecret);
}

/** True when Key ID is a Razorpay Test Mode key (prefix rzp_test_). */
export function isRazorpayTestKeyId(keyId: string): boolean {
  return keyId.startsWith('rzp_test_');
}

export function razorpayConfigurationMessage(config: ConfigService): string | null {
  if (!isLicensePurchaseEnabled(config)) {
    return 'Organisation licence purchases are disabled (LICENSE_PURCHASE_ENABLED=false).';
  }
  const { keyId, keySecret } = readRazorpayKeys(config);
  if (!keyId && !keySecret) {
    return 'Razorpay is not configured. Add RAZORPAY_KEY_ID and RAZORPAY_KEY_SECRET to PartFinder-Backend/.env (Test Mode keys from https://dashboard.razorpay.com/app/keys).';
  }
  if (!keyId) {
    return 'RAZORPAY_KEY_ID is missing in PartFinder-Backend/.env.';
  }
  if (!keySecret) {
    return 'RAZORPAY_KEY_SECRET is missing in PartFinder-Backend/.env.';
  }
  return null;
}
