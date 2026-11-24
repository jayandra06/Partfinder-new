import { Logger } from '@nestjs/common';
import { Client } from '@elastic/elasticsearch';
import { EsPartDocument, ElasticsearchService } from './elasticsearch.service';

jest.mock('@elastic/elasticsearch', () => ({ Client: jest.fn() }));
const ClientMock = Client as unknown as jest.Mock;

function fakeClient() {
  return {
    ping: jest.fn().mockResolvedValue(true),
    index: jest.fn().mockResolvedValue({}),
    bulk: jest.fn().mockResolvedValue({ errors: false, items: [] }),
    delete: jest.fn().mockResolvedValue({}),
    search: jest.fn(),
    deleteByQuery: jest.fn().mockResolvedValue({}),
    close: jest.fn().mockResolvedValue(undefined),
    indices: { exists: jest.fn().mockResolvedValue(true), create: jest.fn().mockResolvedValue({}) },
  };
}

const config = (url?: string) => ({ get: jest.fn(() => url) }) as any;
const doc = (id: string, createdAt?: string): EsPartDocument => ({
  partId: id, orgId: 'o1', templateId: 't1', rowId: `r-${id}`, metadata: { Brand: 'Bosch' }, allText: 'bosch x1', createdAt,
});

describe('ElasticsearchService', () => {
  let client: ReturnType<typeof fakeClient>;
  let warn: jest.SpyInstance;

  beforeEach(() => {
    client = fakeClient();
    ClientMock.mockReset().mockImplementation(() => client);
    warn = jest.spyOn(Logger.prototype, 'warn').mockImplementation(() => undefined);
    jest.spyOn(Logger.prototype, 'log').mockImplementation(() => undefined);
  });
  afterEach(() => jest.restoreAllMocks());

  it('is disabled (no-ops) when ELASTICSEARCH_URL is unset, and only checks once', async () => {
    const cfg = config(undefined);
    const svc = new ElasticsearchService(cfg);

    await expect(svc.searchParts('o1', 'x')).resolves.toEqual({ total: 0, ids: [] });
    await svc.indexPart(doc('p1'));
    expect(svc.isAvailable()).toBe(false);
    expect(ClientMock).not.toHaveBeenCalled();
    expect(cfg.get).toHaveBeenCalledTimes(1);
  });

  it('stays disabled when the ping fails', async () => {
    client.ping.mockRejectedValue(new Error('ECONNREFUSED'));
    const svc = new ElasticsearchService(config('http://es:9200'));

    await svc.removePart('p1');

    expect(svc.isAvailable()).toBe(false);
    expect(client.delete).not.toHaveBeenCalled();
    expect(warn).toHaveBeenCalledWith(expect.stringContaining('ECONNREFUSED'));
  });

  it('connects, creates the index when missing, and indexes a part', async () => {
    client.indices.exists.mockResolvedValue(false);
    const svc = new ElasticsearchService(config(' http://es:9200 '));

    await svc.indexPart(doc('p1', '2026-01-01T00:00:00Z'));

    expect(ClientMock).toHaveBeenCalledWith({ node: 'http://es:9200' });
    expect(svc.isAvailable()).toBe(true);
    expect(client.indices.create).toHaveBeenCalledWith(expect.objectContaining({ index: 'partfinder_parts' }));
    expect(client.index).toHaveBeenCalledWith({
      index: 'partfinder_parts',
      id: 'p1',
      document: { orgId: 'o1', templateId: 't1', rowId: 'r-p1', metadata: { Brand: 'Bosch' }, allText: 'bosch x1', createdAt: '2026-01-01T00:00:00Z' },
    });
  });

  it('builds a filtered, paged multi_match search and handles both total shapes', async () => {
    const svc = new ElasticsearchService(config('http://es'));
    client.search.mockResolvedValueOnce({ hits: { total: { value: 42 }, hits: [{ _id: 'a' }, { _id: 'b' }] } });

    await expect(svc.searchParts('o1', ' bosch ', ' t1 ', 3, 20)).resolves.toEqual({ total: 42, ids: ['a', 'b'] });
    const req = client.search.mock.calls[0][0];
    expect(req).toMatchObject({ index: 'partfinder_parts', size: 20, from: 40, _source: false });
    expect(req.query.bool.must).toEqual([
      { term: { orgId: 'o1' } },
      { term: { templateId: 't1' } },
      { multi_match: { query: 'bosch', fields: ['allText', 'rowId', 'metadata.*'], type: 'best_fields', fuzziness: 'AUTO' } },
    ]);

    client.search.mockResolvedValueOnce({ hits: { total: 3, hits: [] } });
    await expect(svc.searchParts('o1', '', undefined, 0)).resolves.toEqual({ total: 3, ids: [] });
    const req2 = client.search.mock.calls[1][0];
    expect(req2.from).toBe(0);
    expect(req2.query.bool.must).toEqual([{ term: { orgId: 'o1' } }]);
  });

  it('returns an empty result when search throws', async () => {
    const svc = new ElasticsearchService(config('http://es'));
    client.search.mockRejectedValue(new Error('boom'));

    await expect(svc.searchParts('o1', 'q')).resolves.toEqual({ total: 0, ids: [] });
  });

  it('bulk indexes pairs of operations and logs partial failures', async () => {
    const svc = new ElasticsearchService(config('http://es'));
    client.bulk.mockResolvedValue({ errors: true, items: [{ index: { error: {} } }, { index: {} }] });

    await svc.bulkIndexParts([doc('p1', 'x'), doc('p2', 'y')]);

    const { operations, refresh } = client.bulk.mock.calls[0][0];
    expect(refresh).toBe(false);
    expect(operations).toHaveLength(4);
    expect(operations[0]).toEqual({ index: { _index: 'partfinder_parts', _id: 'p1' } });
    expect(operations[3].rowId).toBe('r-p2');
    expect(warn).toHaveBeenCalledWith('Bulk index: 1 of 2 parts failed');
  });

  it('ignores 404s on delete but logs other errors', async () => {
    const svc = new ElasticsearchService(config('http://es'));
    client.delete.mockRejectedValueOnce({ meta: { statusCode: 404 }, message: 'not found' });
    await svc.removePart('gone');
    expect(warn).not.toHaveBeenCalledWith(expect.stringContaining('ES delete failed'));

    client.delete.mockRejectedValueOnce(new Error('timeout'));
    await svc.removePart('p1');
    expect(warn).toHaveBeenCalledWith('ES delete failed: timeout');
  });

  it('reindexOrg deletes the org documents then bulk indexes, even if cleanup fails', async () => {
    const svc = new ElasticsearchService(config('http://es'));
    client.deleteByQuery.mockRejectedValue(new Error('nope'));

    await svc.reindexOrg('o1', [doc('p1')]);

    expect(client.deleteByQuery).toHaveBeenCalledWith({ index: 'partfinder_parts', query: { term: { orgId: 'o1' } }, refresh: true });
    expect(client.bulk).toHaveBeenCalledTimes(1);
  });

  it('closes the client on module destroy', async () => {
    const svc = new ElasticsearchService(config('http://es'));
    await svc.ensureIndex();

    await svc.onModuleDestroy();

    expect(client.close).toHaveBeenCalled();
    expect(svc.isAvailable()).toBe(false);
  });
});
