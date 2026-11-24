import { replaceMongoDatabasePath } from './org-database-uri.util';

describe('org-database-uri.util', () => {
  it('replaces db segment before query', () => {
    const u = replaceMongoDatabasePath('mongodb://h/mydb?retry=true', 'ORG99');
    expect(u).toContain('/ORG99?');
    expect(u).toContain('retry=true');
  });
});
