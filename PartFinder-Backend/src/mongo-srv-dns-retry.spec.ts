import { isLikelySrvDnsFailure } from './mongo-srv-dns-retry';

describe('mongo-srv-dns-retry', () => {
  it('detects SRV DNS failures', () => {
    expect(isLikelySrvDnsFailure(new Error('querySrv ETIMEOUT'))).toBe(true);
    expect(isLikelySrvDnsFailure(new Error('auth failed'))).toBe(false);
  });
});
