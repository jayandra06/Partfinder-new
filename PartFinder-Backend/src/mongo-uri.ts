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