import { createHmac } from 'crypto';
import { verifyRazorpayCheckoutSignature } from './license-purchase.signature';

describe('license-purchase.signature', () => {
  it('accepts valid signature', () => {
    const secret = 'test_secret';
    const order = 'order_1';
    const pay = 'pay_1';
    const sig = createHmac('sha256', secret).update(`${order}|${pay}`).digest('hex');
    expect(verifyRazorpayCheckoutSignature(order, pay, sig, secret)).toBe(true);
  });

  it('rejects tampered signature', () => {
    expect(verifyRazorpayCheckoutSignature('o', 'p', 'deadbeef', 'secret')).toBe(false);
  });
});
