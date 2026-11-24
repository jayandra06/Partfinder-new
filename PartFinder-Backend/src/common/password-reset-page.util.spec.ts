import { buildPasswordResetHtmlPage } from './password-reset-page.util';

describe('password-reset-page.util', () => {
  it('escapes token in HTML', () => {
    const html = buildPasswordResetHtmlPage('<script>', 'https://api.test/post');
    // The page ships exactly one inline <script> of its own; the token must not add another.
    expect(html.match(/<script>/g)).toHaveLength(1);
    expect(html).toContain('<input type="hidden" id="token" value="&lt;script&gt;" />');
  });

  it('escapes quotes and ampersands so the token cannot break out of the attribute', () => {
    const html = buildPasswordResetHtmlPage('a"b&c', 'https://api.test/post');
    expect(html).toContain('value="a&quot;b&amp;c"');
    expect(html).not.toContain('value="a"b');
  });
});
