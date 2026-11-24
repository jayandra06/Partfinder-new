import { BadRequestException, Logger, NotFoundException } from '@nestjs/common';
import { Types } from 'mongoose';
import { RedisService } from '../common/redis/redis.service';
import { PartsService } from './parts.service';

/** Chainable stand-in for a mongoose Query: sort/skip/limit return itself, lean resolves. */
function q<T>(value: T) {
  const query: any = {};
  for (const m of ['sort', 'skip', 'limit']) query[m] = jest.fn().mockReturnValue(query);
  query.lean = jest.fn().mockResolvedValue(value);
  return query;
}

function setup(esAvailable = false) {
  const templateId = new Types.ObjectId().toString();
  const partId = new Types.ObjectId();
  const part = { _id: partId, orgId: 'org1', templateId, rowId: 'ROW-1', metadata: { brand: 'Bosch', quantity: '3' }, createdAt: new Date('2026-01-02T03:04:05Z') };
  const parts = {
    find: jest.fn().mockReturnValue(q([part])),
    countDocuments: jest.fn().mockResolvedValue(1),
    create: jest.fn().mockResolvedValue({ toObject: () => part }),
    findOne: jest.fn().mockReturnValue(q(part)),
    findOneAndUpdate: jest.fn().mockReturnValue(q(part)),
    findOneAndDelete: jest.fn().mockReturnValue(q(part)),
  };
  const es = {
    isAvailable: jest.fn().mockReturnValue(esAvailable),
    searchParts: jest.fn().mockResolvedValue({ ids: [], total: 0 }),
    indexPart: jest.fn().mockResolvedValue(undefined),
    removePart: jest.fn().mockResolvedValue(undefined),
    reindexOrg: jest.fn().mockResolvedValue(undefined),
  };
  const redis = new RedisService({ get: () => undefined } as any);
  const service = new PartsService(parts as any, redis, es as any);
  return { service, parts, es, redis, part, partId: partId.toString(), templateId };
}

describe('PartsService', () => {
  beforeAll(() => jest.spyOn(Logger.prototype, 'warn').mockImplementation(() => undefined));
  afterAll(() => jest.restoreAllMocks());

  it('list clamps paging and filters by org and template', async () => {
    const { service, parts, templateId } = setup();

    const result = await service.list('org1', ` ${templateId} `, undefined, 0, 9999);

    expect(result).toMatchObject({ page: 1, limit: 500, total: 1 });
    expect(parts.find).toHaveBeenCalledWith({ orgId: 'org1', templateId });
    const query = parts.find.mock.results[0].value;
    expect(query.skip).toHaveBeenCalledWith(0);
    expect(query.limit).toHaveBeenCalledWith(500);
  });

  it('list computes skip from page and limit', async () => {
    const { service, parts } = setup();

    await service.list('org1', undefined, undefined, 3, 20);

    expect(parts.find.mock.results[0].value.skip).toHaveBeenCalledWith(40);
  });

  it('list caches non-search results', async () => {
    const { service, parts } = setup();

    await service.list('org1');
    await service.list('org1');

    expect(parts.find).toHaveBeenCalledTimes(1);
  });

  it('list falls back to a case-insensitive rowId regex when ES is unavailable', async () => {
    const { service, parts, es } = setup(false);

    await service.list('org1', undefined, ' row ');
    await service.list('org1', undefined, ' row ');

    expect(es.searchParts).not.toHaveBeenCalled();
    expect(parts.find).toHaveBeenCalledWith({ orgId: 'org1', $or: [{ rowId: { $regex: 'row', $options: 'i' } }] });
    expect(parts.find).toHaveBeenCalledTimes(2);
  });

  it('list uses Elasticsearch when available and keeps its ranking order', async () => {
    const { service, parts, es } = setup(true);
    const a = { _id: new Types.ObjectId() };
    const b = { _id: new Types.ObjectId() };
    es.searchParts.mockResolvedValue({ ids: [b._id.toString(), a._id.toString()], total: 2 });
    parts.find.mockReturnValue(q([a, b]));

    const result = await service.list('org1', undefined, 'bosch', 1, 10);

    expect(es.searchParts).toHaveBeenCalledWith('org1', 'bosch', undefined, 1, 10);
    expect(result.items.map((i: any) => i._id)).toEqual([b._id, a._id]);
    expect(result.total).toBe(2);
  });

  it('list returns empty items when Elasticsearch finds nothing', async () => {
    const { service, parts, es } = setup(true);
    es.searchParts.mockResolvedValue({ ids: [], total: 0 });

    await expect(service.list('org1', undefined, 'zzz')).resolves.toEqual({ page: 1, limit: 100, total: 0, items: [] });
    expect(parts.find).not.toHaveBeenCalled();
  });

  it('create validates templateId', async () => {
    const { service } = setup();

    await expect(service.create('org1', { templateId: 'bad', rowId: 'r' } as any)).rejects.toBeInstanceOf(BadRequestException);
  });

  it('create stores the part and indexes a searchable document', async () => {
    const { service, parts, es, templateId, partId } = setup();

    await service.create('org1', { templateId, rowId: 'ROW-1' } as any);

    expect(parts.create).toHaveBeenCalledWith({ orgId: 'org1', templateId, rowId: 'ROW-1', metadata: {} });
    expect(es.indexPart).toHaveBeenCalledWith({
      partId,
      orgId: 'org1',
      templateId,
      rowId: 'ROW-1',
      metadata: { brand: 'Bosch', quantity: '3' },
      allText: 'ROW-1 Bosch 3',
      createdAt: '2026-01-02T03:04:05.000Z',
    });
  });

  it('getById caches and 404s on missing parts', async () => {
    const { service, parts, partId } = setup();

    await service.getById('org1', partId);
    await service.getById('org1', partId);
    expect(parts.findOne).toHaveBeenCalledTimes(1);

    parts.findOne.mockReturnValue(q(null));
    await expect(service.getById('org1', new Types.ObjectId().toString())).rejects.toBeInstanceOf(NotFoundException);
  });

  it('update invalidates the cached part', async () => {
    const { service, parts, partId } = setup();
    await service.getById('org1', partId);

    await service.update('org1', partId, { metadata: { brand: 'Valeo' } } as any);
    await service.getById('org1', partId);

    expect(parts.findOneAndUpdate).toHaveBeenCalledWith({ _id: partId, orgId: 'org1' }, { $set: { metadata: { brand: 'Valeo' } } }, { new: true });
    expect(parts.findOne).toHaveBeenCalledTimes(2);
  });

  it('update and remove 404 when the part is missing', async () => {
    const { service, parts, partId } = setup();
    parts.findOneAndUpdate.mockReturnValue(q(null));
    parts.findOneAndDelete.mockReturnValue(q(null));

    await expect(service.update('org1', partId, {} as any)).rejects.toBeInstanceOf(NotFoundException);
    await expect(service.remove('org1', partId)).rejects.toBeInstanceOf(NotFoundException);
  });

  it('remove deletes from Elasticsearch', async () => {
    const { service, es, partId } = setup();

    await expect(service.remove('org1', partId)).resolves.toEqual({ deleted: true });
    expect(es.removePart).toHaveBeenCalledWith(partId);
  });

  it('lowStock counts quantities strictly between 0 and 10', async () => {
    const { service, parts } = setup();
    const mk = (metadata: Record<string, unknown>) => ({ _id: new Types.ObjectId(), metadata });
    parts.find.mockReturnValue(q([mk({ quantity: '3' }), mk({ qty: 9 }), mk({ quantity: 0 }), mk({ quantity: 10 }), mk({ quantity: 'n/a' }), mk({})]));

    const result: any = await service.lowStock('org1');

    expect(result.count).toBe(2);
  });

  it('syncToElasticsearch reindexes every org part', async () => {
    const { service, es } = setup();

    await expect(service.syncToElasticsearch('org1')).resolves.toEqual({ synced: 1 });
    expect(es.reindexOrg).toHaveBeenCalledWith('org1', [expect.objectContaining({ rowId: 'ROW-1', orgId: 'org1' })]);
  });
});
