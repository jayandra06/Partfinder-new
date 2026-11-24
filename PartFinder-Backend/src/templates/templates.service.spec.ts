import { BadRequestException, Logger, NotFoundException } from '@nestjs/common';
import { Types } from 'mongoose';
import { RedisService } from '../common/redis/redis.service';
import { TemplatesService } from './templates.service';

function q<T>(value: T) {
  const query: any = {};
  for (const m of ['sort', 'skip', 'limit']) query[m] = jest.fn().mockReturnValue(query);
  query.lean = jest.fn().mockResolvedValue(value);
  return query;
}

const oid = () => new Types.ObjectId().toString();

function setup() {
  const templateId = oid();
  const colA = oid();
  const colB = oid();
  const rowId = new Types.ObjectId();
  const template = { _id: templateId, orgId: 'org1', name: 'Brakes' };
  const templates = {
    create: jest.fn().mockResolvedValue({ toObject: () => template }),
    find: jest.fn().mockReturnValue(q([template])),
    findOne: jest.fn().mockReturnValue(q(template)),
    findOneAndUpdate: jest.fn().mockReturnValue(q({ ...template, name: 'Renamed' })),
    findOneAndDelete: jest.fn().mockReturnValue(q(template)),
  };
  const columns = {
    create: jest.fn().mockImplementation(async (d: any) => ({ toObject: () => d })),
    find: jest.fn().mockReturnValue(q([{ _id: colA }, { _id: colB }])),
    findOneAndUpdate: jest.fn().mockReturnValue(q({ _id: colA })),
    findOneAndDelete: jest.fn().mockReturnValue(q({ _id: colA })),
    deleteMany: jest.fn().mockResolvedValue({}),
  };
  const rows = {
    create: jest.fn().mockResolvedValue({ _id: rowId }),
    find: jest.fn().mockReturnValue(q([{ _id: rowId }])),
    findById: jest.fn().mockReturnValue(q({ _id: rowId, templateId })),
    findOne: jest.fn().mockReturnValue(q({ _id: rowId })),
    findOneAndDelete: jest.fn().mockReturnValue(q({ _id: rowId })),
    countDocuments: jest.fn().mockResolvedValue(1),
    deleteMany: jest.fn().mockResolvedValue({}),
  };
  const cells = {
    insertMany: jest.fn().mockResolvedValue([]),
    find: jest.fn().mockReturnValue(q([{ rowId: rowId.toString(), columnId: colA, value: 'Bosch' }])),
    findOneAndUpdate: jest.fn().mockResolvedValue({}),
    deleteMany: jest.fn().mockResolvedValue({}),
  };
  const redis = new RedisService({ get: () => undefined } as any);
  const service = new TemplatesService(templates as any, columns as any, rows as any, cells as any, redis);
  return { service, templates, columns, rows, cells, templateId, colA, colB, rowId: rowId.toString() };
}

describe('TemplatesService', () => {
  beforeAll(() => jest.spyOn(Logger.prototype, 'warn').mockImplementation(() => undefined));
  afterAll(() => jest.restoreAllMocks());

  it('create trims the name', async () => {
    const { service, templates } = setup();

    await service.create('org1', { name: '  Brakes ' } as any);

    expect(templates.create).toHaveBeenCalledWith({ orgId: 'org1', name: 'Brakes' });
  });

  it('list is cached and invalidated by rename', async () => {
    const { service, templates, templateId } = setup();

    await service.list('org1');
    await service.list('org1');
    expect(templates.find).toHaveBeenCalledTimes(1);

    await service.rename('org1', templateId, { name: ' Renamed ' } as any);
    await service.list('org1');
    expect(templates.find).toHaveBeenCalledTimes(2);
    expect(templates.findOneAndUpdate).toHaveBeenCalledWith({ _id: templateId, orgId: 'org1' }, { $set: { name: 'Renamed' } }, { new: true });
  });

  it('getOne includes columns sorted by order', async () => {
    const { service, columns, templateId } = setup();

    const result: any = await service.getOne('org1', templateId);

    expect(result.name).toBe('Brakes');
    expect(result.columns).toHaveLength(2);
    expect(columns.find.mock.results[0].value.sort).toHaveBeenCalledWith({ order: 1 });
  });

  it('rejects invalid ids and missing templates', async () => {
    const { service, templates } = setup();

    await expect(service.getOne('org1', 'nope')).rejects.toBeInstanceOf(BadRequestException);
    templates.findOne.mockReturnValue(q(null));
    await expect(service.getOne('org1', oid())).rejects.toBeInstanceOf(NotFoundException);
    await expect(service.addColumn('org1', oid(), { name: 'x', order: 0 } as any)).rejects.toBeInstanceOf(NotFoundException);
  });

  it('delete cascades to columns, rows and their cells', async () => {
    const { service, columns, rows, cells, templateId, rowId } = setup();

    await expect(service.delete('org1', templateId)).resolves.toEqual({ deleted: true });
    expect(columns.deleteMany).toHaveBeenCalledWith({ templateId });
    expect(rows.deleteMany).toHaveBeenCalledWith({ templateId, orgId: 'org1' });
    expect(cells.deleteMany).toHaveBeenCalledWith({ rowId: { $in: [rowId] } });
  });

  it('addColumn defaults to text and flags link columns', async () => {
    const { service, columns, templateId } = setup();

    const text: any = await service.addColumn('org1', templateId, { name: ' Brand ', order: 1 } as any);
    const link: any = await service.addColumn('org1', templateId, { name: 'Vendor', order: 2, type: 'link' } as any);

    expect(text).toMatchObject({ name: 'Brand', type: 'text', isLink: false });
    expect(link).toMatchObject({ type: 'link', isLink: true });
    expect(columns.create).toHaveBeenCalledTimes(2);
  });

  it('updateColumn only sets provided fields', async () => {
    const { service, columns, templateId, colA } = setup();

    await service.updateColumn('org1', templateId, colA, { type: 'link' } as any);

    expect(columns.findOneAndUpdate).toHaveBeenCalledWith({ _id: colA, templateId }, { $set: { type: 'link', isLink: true } }, { new: true });
  });

  it('deleteColumn removes that column cells from every row', async () => {
    const { service, cells, templateId, colA, rowId } = setup();

    await service.deleteColumn('org1', templateId, colA);

    expect(cells.deleteMany).toHaveBeenCalledWith({ rowId: { $in: [rowId] }, columnId: colA });
  });

  it('addRow ignores cells for unknown columns and defaults empty values', async () => {
    const { service, cells, templateId, colA, colB, rowId } = setup();

    const row: any = await service.addRow('org1', templateId, {
      cells: [{ columnId: colA, value: 'Bosch' }, { columnId: colB }, { columnId: oid(), value: 'ignored' }],
    } as any);

    expect(cells.insertMany).toHaveBeenCalledWith(
      [
        { rowId, columnId: colA, value: 'Bosch' },
        { rowId, columnId: colB, value: '' },
      ],
      { ordered: false },
    );
    expect(row.cells).toEqual([{ columnId: colA, value: 'Bosch' }]);
  });

  it('getRows clamps paging and groups cells by row', async () => {
    const { service, rows, templateId, colA, rowId } = setup();

    const result: any = await service.getRows('org1', templateId, -5, 0);

    expect(result).toMatchObject({ page: 1, limit: 1, total: 1 });
    expect(rows.find.mock.results[0].value.skip).toHaveBeenCalledWith(0);
    expect(result.rows[0].cells).toEqual([{ columnId: colA, value: 'Bosch' }]);
    expect(String(result.rows[0]._id)).toBe(rowId);
  });

  it('updateRow upserts each provided cell', async () => {
    const { service, cells, templateId, colA, rowId } = setup();

    await service.updateRow('org1', templateId, rowId, { cells: [{ columnId: colA, value: 'Valeo' }] } as any);

    expect(cells.findOneAndUpdate).toHaveBeenCalledWith({ rowId, columnId: colA }, { $set: { value: 'Valeo' } }, { upsert: true, new: true });
  });

  it('updateRow and deleteRow 404 for missing rows', async () => {
    const { service, rows, templateId } = setup();
    rows.findOne.mockReturnValue(q(null));
    rows.findOneAndDelete.mockReturnValue(q(null));

    await expect(service.updateRow('org1', templateId, oid(), { cells: [] } as any)).rejects.toBeInstanceOf(NotFoundException);
    await expect(service.deleteRow('org1', templateId, oid())).rejects.toBeInstanceOf(NotFoundException);
  });

  it('deleteRow removes the row cells', async () => {
    const { service, cells, templateId, rowId } = setup();

    await expect(service.deleteRow('org1', templateId, rowId)).resolves.toEqual({ deleted: true });
    expect(cells.deleteMany).toHaveBeenCalledWith({ rowId });
  });
});
