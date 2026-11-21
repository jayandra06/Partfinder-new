import { PARTFINDER_MICROSOFT_STORE_URL, resolveInviteDownloadLink } from './invite-download-link.util';

describe('invite-download-link.util', () => {
  it('defaults to Microsoft Store', () => {
    expect(resolveInviteDownloadLink(undefined)).toBe(PARTFINDER_MICROSOFT_STORE_URL);
  });

  it('migrates legacy shipspan URLs', () => {
    expect(resolveInviteDownloadLink('https://shipspan.com/foo')).toBe(PARTFINDER_MICROSOFT_STORE_URL);
  });
});
