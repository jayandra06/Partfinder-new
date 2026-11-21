import { buildPasswordResetHtmlPage } from './password-reset-page.util';

describe('password-reset-page.util', () => {
  it('escapes token in HTML', () => {
    const html = buildPasswordResetHtmlPage('<script>', 'https://api.test/post');
    expect(html).not.toContain('<script>');
    expect(html).toContain('&lt;script&gt;');
  });
});
