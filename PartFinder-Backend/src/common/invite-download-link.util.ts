/** Official PartFinder listing on Microsoft Store (Windows app). */
export const PARTFINDER_MICROSOFT_STORE_URL =
  'https://apps.microsoft.com/detail/9n6xvkpx189w?hl=en-GB&gl=IN';

/** Env override, with legacy shipspan URLs migrated to the Store link. */
export function resolveInviteDownloadLink(configured?: string | null): string {
  const trimmed = configured?.trim() ?? '';
  if (!trimmed) {
    return PARTFINDER_MICROSOFT_STORE_URL;
  }

  if (/shipspan\.com/i.test(trimmed)) {
    return PARTFINDER_MICROSOFT_STORE_URL;
  }

  return trimmed;
}
