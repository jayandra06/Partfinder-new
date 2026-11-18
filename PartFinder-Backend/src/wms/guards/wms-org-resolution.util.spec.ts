import { ForbiddenException } from '@nestjs/common';
import { resolveAuthoritativeOrgCode } from './wms-org-resolution.util';

describe('resolveAuthoritativeOrgCode', () => {
  it('returns JWT org when header matches', () => {
    expect(
      resolveAuthoritativeOrgCode(
        { email: 'a@b.com', orgCode: '884470' },
        '884470',
      ),
    ).toBe('884470');
  });

  it('returns JWT org when header is empty', () => {
    expect(
      resolveAuthoritativeOrgCode({ email: 'a@b.com', orgCode: '123456' }, ''),
    ).toBe('123456');
  });

  it('rejects legacy JWT without orgCode', () => {
    expect(() =>
      resolveAuthoritativeOrgCode({ email: 'a@b.com' }, '884470'),
    ).toThrow(ForbiddenException);
  });

  it('rejects cross-tenant header', () => {
    expect(() =>
      resolveAuthoritativeOrgCode(
        { email: 'a@b.com', orgCode: '111111' },
        '222222',
      ),
    ).toThrow(/does not match/i);
  });

  it('rejects malformed header when provided', () => {
    expect(() =>
      resolveAuthoritativeOrgCode({ email: 'a@b.com', orgCode: '111111' }, 'abc'),
    ).toThrow(ForbiddenException);
  });
});