/** Strip BOM / whitespace so Mongoose always sees a valid scheme prefix. */
export function normalizeMongoUri(raw: string | undefined): string {
  if (raw === undefined || raw === null) {
    return '';
  }
  let s = String(raw).trim();
  if (s.charCodeAt(0) === 0xfeff) {
    s = s.slice(1).trim();
  }
  return s;
}

export function assertMongoUri(uri: string): void {
  if (
    !uri ||
    (!uri.startsWith('mongodb://') && !uri.startsWith('mongodb+srv://'))
  ) {
    throw new Error(
      'MONGODB_URI is missing or invalid. Set it in PartFinder-Backend/.env to a single line starting with mongodb:// or mongodb+srv://, then save the file.',
    );
  }
}

/**
 * `mongodb+srv://` implies TLS. Plain `mongodb://` does not unless `tls` / `ssl` is set.
 * MongoDB Atlas (`*.mongodb.net`) requires TLS — matches WinUI `MongoConnectionStringUtil`.
 */
export function ensureAtlasTlsForStandardUri(uri: string): string {
  const s = uri.trim();
  if (s.startsWith('mongodb+srv://')) {
    return s;
  }
  if (!s.startsWith('mongodb://')) {
    return s;
  }
  if (!s.includes('.mongodb.net')) {
    return s;
  }
  const q = s.indexOf('?');
  const tail = q >= 0 ? s.slice(q + 1) : '';
  const lower = tail.toLowerCase();
  const hasTls =
    /\btls=(true|1)\b/.test(lower) || /\bssl=(true|1)\b/.test(lower);
  if (hasTls) {
    return s;
  }
  return q >= 0 ? `${s}&tls=true` : `${s}?tls=true`;
}

/** Normalize + Atlas TLS fix for driver connections (admin DB clusters, probes). */
export function prepareMongoUriForDriver(raw: string | undefined): string {
  return ensureAtlasTlsForStandardUri(normalizeMongoUri(raw));
}