import { fromBase32, isPlausibleTotpSecretBase32, verifyTotp } from './totp.util';

describe('totp.util', () => {
  it('rejects empty base32', () => {
    expect(() => fromBase32('')).toThrow('empty');
  });

  it('detects plausible secrets', () => {
    expect(isPlausibleTotpSecretBase32('JBSWY3DPEHPK3PXP')).toBe(true);
    expect(isPlausibleTotpSecretBase32('!!!')).toBe(false);
  });

  it('rejects non-six-digit codes', () => {
    expect(verifyTotp('JBSWY3DPEHPK3PXP', '12345')).toBe(false);
  });
});
