/**
 * Replace the database segment in a Mongo URI (used for default provisioning: db name = org code).
 */
export function replaceMongoDatabasePath(uri: string, newDbName: string): string {
  const trimmed = uri.trim();
  const qIndex = trimmed.indexOf('?');
  const query = qIndex >= 0 ? trimmed.slice(qIndex) : '';
  const withoutQuery = qIndex >= 0 ? trimmed.slice(0, qIndex) : trimmed;
  const schemeIdx = withoutQuery.indexOf('//');
  if (schemeIdx < 0) {
    return `${trimmed}/${encodeURIComponent(newDbName)}`;
  }
  const pathStart = withoutQuery.indexOf('/', schemeIdx + 2);
  if (pathStart < 0) {
    return `${withoutQuery}/${encodeURIComponent(newDbName)}${query}`;
  }
  return `${withoutQuery.slice(0, pathStart)}/${encodeURIComponent(newDbName)}${query}`;
}
