import { ConfigService } from '@nestjs/config';
import {
  isLicensePurchaseEnabled,
  isRazorpayConfigured,
  isRazorpayTestKeyId,
  razorpayConfigurationMessage,
} from './license-purchase.config';

function mockConfig(values: Record<string, string | undefined>): ConfigService {
  return {
    get: (key: string) => values[key],
  } as ConfigService;
}

describe('license-purchase.config', () => {
  it('treats licence purchase as enabled by default', () => {
    expect(isLicensePurchaseEnabled(mockConfig({}))).toBe(true);
  });

  it('respects LICENSE_PURCHASE_ENABLED=false', () => {
    expect(isLicensePurchaseEnabled(mockConfig({ LICENSE_PURCHASE_ENABLED: 'false' }))).toBe(
      false,
    );
  });

  it('detects missing Razorpay credentials', () => {
    const config = mockConfig({ LICENSE_PURCHASE_ENABLED: 'true' });
    expect(isRazorpayConfigured(config)).toBe(false);
    expect(razorpayConfigurationMessage(config)).toMatch(/RAZORPAY_KEY_ID/);
  });

  it('detects configured test keys without requiring both to be real', () => {
    const config = mockConfig({
      RAZORPAY_KEY_ID: 'rzp_test_abc',
      RAZORPAY_KEY_SECRET: 'realistic_test_secret_value',
    });
    expect(isRazorpayConfigured(config)).toBe(true);
    expect(isRazorpayTestKeyId('rzp_test_abc')).toBe(true);
    expect(razorpayConfigurationMessage(config)).toBeNull();
  });

  it('rejects .env.example placeholder credentials', () => {
    const config = mockConfig({
      RAZORPAY_KEY_ID: 'rzp_test_your_key_id_here',
      RAZORPAY_KEY_SECRET: 'your_test_key_secret_here',
    });
    expect(isRazorpayConfigured(config)).toBe(false);
  });
});
