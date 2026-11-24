import { assertMongoUri, ensureAtlasTlsForStandardUri, normalizeMongoUri } from './mongo-uri';

describe('mongo-uri', () => {
  it('trims and strips BOM', () => {
    expect(normalizeMongoUri('  mongodb://localhost  ')).toBe('mongodb://localhost');
  });

  it('adds tls for Atlas host', () => {
    const u = ensureAtlasTlsForStandardUri('mongodb://cluster.mongodb.net/db');
    expect(u).toContain('tls=true');
  });

  it('assertMongoUri throws for invalid', () => {
    expect(() => assertMongoUri('http://bad')).toThrow(/MONGODB_URI/);
  });
});
