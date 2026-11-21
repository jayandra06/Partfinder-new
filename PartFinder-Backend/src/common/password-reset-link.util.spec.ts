import { ConfigService } from '@nestjs/config';
import { buildPasswordResetLink, resolvePasswordResetFormBaseUrl } from './password-reset-link.util';

describe('password-reset-link.util', () => {
  it('uses explicit PASSWORD_RESET_BASE_URL', () => {
    const config = {
      get: (k: string) => (k === 'PASSWORD_RESET_BASE_URL' ? 'https://x.test/form' : undefined),
    } as ConfigService;
    expect(resolvePasswordResetFormBaseUrl(config)).toBe('https://x.test/form');
  });

  it('builds token query', () => {
    const config = { get: () => undefined } as unknown as ConfigService;
    const link = buildPasswordResetLink(config, 'abc 123');
    expect(link).toContain('token=abc%20123');
  });
});
