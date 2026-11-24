import { BadRequestException, Logger, NotFoundException } from '@nestjs/common';
import { Types } from 'mongoose';
import { RedisService } from '../common/redis/redis.service';
import { RelationsService } from './relations.service';

const lean = <T>(value: T) => ({ lean: jest.fn().mockResolvedValue(value) });

function setup() {
  const id = new Types.ObjectId().toString();
  const relationDoc = {
    _id: id,
    orgId: 'org1',
    primaryTemplateId: 'p',
    lookupTemplateId: 'l',
    name: 'Parts to Vendors',
    triggerColumn: 'Vendor',
    menuLabel: 'Vendor info',
  };
  const relations = {
    create: jest.fn().mockResolvedValue({ _id: { toString: () => id } }),
    findOne: jest.fn().mockReturnValue(lean(relationDoc)),
    find: jest.fn().mockReturnValue({ sort: jest.fn().mockReturnValue(lean([relationDoc])) }),
    updateOne: jest.fn().mockResolvedValue({}),
    findOneAndDelete: jest.fn().mockReturnValue(lean(relationDoc)),
  };
  const matchKeys = {
    insertMany: jest.fn().mockResolvedValue([]),
    deleteMany: jest.fn().mockResolvedValue({}),
    find: jest.fn().mockReturnValue(lean([{ sourceColumn: 'Vendor', targetColumn: 'Name' }])),
  };
  const displayColumns = {
    insertMany: jest.fn().mockResolvedValue([]),
    deleteMany: jest.fn().mockResolvedValue({}),
    find: jest.fn().mockReturnValue(lean([{ columnName: 'Phone' }])),
  };
  const redis = new RedisService({ get: () => undefined } as any);
  const service = new RelationsService(relations as any, matchKeys as any, displayColumns as any, redis);
  return { id, service, relations, matchKeys, displayColumns, redis };
}

describe('RelationsService', () => {
  beforeAll(() => jest.spyOn(Logger.prototype, 'warn').mockImplementation(() => undefined));
  afterAll(() => jest.restoreAllMocks());

  it('create trims text, stores keys and columns, and returns the composed relation', async () => {
    const { id, service, relations, matchKeys, displayColumns } = setup();

    const result = await service.create('org1', {
      primaryTemplateId: 'p',
      lookupTemplateId: 'l',
      name: '  Parts to Vendors  ',
      triggerColumn: 'Vendor',
      menuLabel: ' Vendor info ',
      matchKeys: [{ sourceColumn: 'Vendor', targetColumn: 'Name' }],
      displayColumns: ['Phone'],
    } as any);

    expect(relations.create).toHaveBeenCalledWith(expect.objectContaining({ name: 'Parts to Vendors', menuLabel: 'Vendor info', orgId: 'org1' }));
    expect(matchKeys.insertMany).toHaveBeenCalledWith([{ relationId: id, sourceColumn: 'Vendor', targetColumn: 'Name' }]);
    expect(displayColumns.insertMany).toHaveBeenCalledWith([{ relationId: id, columnName: 'Phone' }]);
    expect(result).toEqual({
      id,
      orgId: 'org1',
      primaryTemplateId: 'p',
      lookupTemplateId: 'l',
      name: 'Parts to Vendors',
      triggerColumn: 'Vendor',
      menuLabel: 'Vendor info',
      matchKeys: [{ sourceColumn: 'Vendor', targetColumn: 'Name' }],
      displayColumns: ['Phone'],
    });
  });

  it('create skips empty key/column inserts', async () => {
    const { service, matchKeys, displayColumns } = setup();

    await service.create('org1', { primaryTemplateId: 'p', lookupTemplateId: 'l', name: 'n', triggerColumn: 't', menuLabel: 'm', matchKeys: [], displayColumns: [] } as any);

    expect(matchKeys.insertMany).not.toHaveBeenCalled();
    expect(displayColumns.insertMany).not.toHaveBeenCalled();
  });

  it('getOne rejects malformed ids before querying', async () => {
    const { service, relations } = setup();

    await expect(service.getOne('org1', 'not-an-id')).rejects.toBeInstanceOf(BadRequestException);
    expect(relations.findOne).not.toHaveBeenCalled();
  });

  it('getOne throws NotFound for another org or missing relation', async () => {
    const { id, service, relations } = setup();
    relations.findOne.mockReturnValue(lean(null));

    await expect(service.getOne('org2', id)).rejects.toBeInstanceOf(NotFoundException);
    expect(relations.findOne).toHaveBeenCalledWith({ _id: id, orgId: 'org2' });
  });

  it('list is cached until a write invalidates it', async () => {
    const { id, service, relations } = setup();

    await service.list('org1');
    await service.list('org1');
    expect(relations.find).toHaveBeenCalledTimes(1);

    await service.update('org1', id, { name: 'Renamed' } as any);
    await service.list('org1');
    expect(relations.find).toHaveBeenCalledTimes(2);
  });

  it('update only sets provided fields and replaces keys/columns', async () => {
    const { id, service, relations, matchKeys, displayColumns } = setup();

    await service.update('org1', id, {
      menuLabel: '  New label ',
      matchKeys: [{ sourceColumn: 'SKU', targetColumn: 'SKU' }],
      displayColumns: [],
    } as any);

    expect(relations.updateOne).toHaveBeenCalledWith({ _id: id, orgId: 'org1' }, { $set: { menuLabel: 'New label' } });
    expect(matchKeys.deleteMany).toHaveBeenCalledWith({ relationId: id });
    expect(matchKeys.insertMany).toHaveBeenCalledWith([{ relationId: id, sourceColumn: 'SKU', targetColumn: 'SKU' }]);
    expect(displayColumns.deleteMany).toHaveBeenCalledWith({ relationId: id });
    expect(displayColumns.insertMany).not.toHaveBeenCalled();
  });

  it('update with no changes does not write the relation document', async () => {
    const { id, service, relations } = setup();

    await service.update('org1', id, {} as any);

    expect(relations.updateOne).not.toHaveBeenCalled();
  });

  it('remove deletes children and reports deletion', async () => {
    const { id, service, matchKeys, displayColumns } = setup();

    await expect(service.remove('org1', id)).resolves.toEqual({ deleted: true });
    expect(matchKeys.deleteMany).toHaveBeenCalledWith({ relationId: id });
    expect(displayColumns.deleteMany).toHaveBeenCalledWith({ relationId: id });
  });

  it('remove of a missing relation throws NotFound', async () => {
    const { id, service, relations } = setup();
    relations.findOneAndDelete.mockReturnValue(lean(null));

    await expect(service.remove('org1', id)).rejects.toBeInstanceOf(NotFoundException);
  });

  it('writes invalidate the org view-data cache', async () => {
    const { id, service, redis } = setup();
    await redis.setJson('pf:view-data:org1', { x: 1 });

    await service.remove('org1', id);

    await expect(redis.exists('pf:view-data:org1')).resolves.toBe(false);
  });
});
