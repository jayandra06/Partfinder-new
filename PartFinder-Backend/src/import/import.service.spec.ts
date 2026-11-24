import { BadRequestException, Logger, NotFoundException } from '@nestjs/common';
import { Types } from 'mongoose';
import { RedisService } from '../common/redis/redis.service';
import { ImportService } from './import.service';

function setup(templateExists = true) {
  const templateId = new Types.ObjectId().toString();
  const brandCol = new Types.ObjectId();
  const modelCol = new Types.ObjectId();
  const templates = { findOne: jest.fn().mockReturnValue({ lean: async () => (templateExists ? { _id: templateId } : null) }) };
  const columns = {
    find: jest.fn().mockReturnValue({ lean: async () => [{ _id: brandCol, name: 'Brand' }, { _id: modelCol, name: ' Model ' }] }),
  };
  let n = 0;
  const rows = { create: jest.fn(async () => ({ _id: { toString: () => `row${++n}` } })) };
  const cells = { insertMany: jest.fn().mockResolvedValue([]) };
  const redis = new RedisService({ get: () => undefined } as any);
  const service = new ImportService(templates as any, columns as any, rows as any, cells as any, redis);
  return { service, templateId, rows, cells, brandCol: brandCol.toString(), modelCol: modelCol.toString() };
}

/** Wait for the setImmediate-scheduled import job to finish. */
async function waitForJob(service: ImportService, templateId: string) {
  for (let i = 0; i < 50; i++) {
    const s = await service.getStatus('org1', templateId);
    if (s.status === 'completed' || s.status === 'failed') return s;
    await new Promise((r) => setImmediate(r));
  }
  throw new Error('import did not finish');
}

describe('ImportService', () => {
  beforeAll(() => jest.spyOn(Logger.prototype, 'warn').mockImplementation(() => undefined));
  afterAll(() => jest.restoreAllMocks());

  it('rejects invalid or unknown templates', async () => {
    await expect(setup().service.enqueueImport('org1', 'bad', Buffer.from(''), {})).rejects.toBeInstanceOf(BadRequestException);
    const missing = setup(false);
    await expect(missing.service.enqueueImport('org1', missing.templateId, Buffer.from(''), {})).rejects.toBeInstanceOf(NotFoundException);
  });

  it('getStatus without any job reports an empty completed state', async () => {
    const { service, templateId } = setup();

    await expect(service.getStatus('org1', templateId)).resolves.toMatchObject({ jobId: '', status: 'completed', totalRows: 0 });
  });

  it('imports CSV rows mapping headers to columns case-insensitively', async () => {
    const { service, templateId, rows, cells, brandCol, modelCol } = setup();
    const csv = Buffer.from('\uFEFFMake,Mdl,Ignored\n Bosch ,X1,zzz\n\nValeo,,q\n');

    const { jobId } = await service.enqueueImport('org1', templateId, csv, { Make: 'brand', Mdl: 'MODEL', Ignored: '' });
    const state = await waitForJob(service, templateId);

    expect(state).toMatchObject({ jobId, status: 'completed', totalRows: 2, processedRows: 2, failedRows: 0, errors: [] });
    expect(rows.create).toHaveBeenCalledTimes(2);
    expect(cells.insertMany).toHaveBeenNthCalledWith(
      1,
      [
        { rowId: 'row1', columnId: brandCol, value: 'Bosch' },
        { rowId: 'row1', columnId: modelCol, value: 'X1' },
      ],
      { ordered: false },
    );
    expect(cells.insertMany.mock.calls[1][0][1]).toEqual({ rowId: 'row2', columnId: modelCol, value: '' });
  });

  it('fails the job when no header maps to a known column', async () => {
    const { service, templateId, rows } = setup();

    await service.enqueueImport('org1', templateId, Buffer.from('A\n1\n'), { A: 'Nope' });
    const state = await waitForJob(service, templateId);

    expect(state.status).toBe('failed');
    expect(state.errors).toEqual(['No valid CSV header mappings were provided.']);
    expect(state.failedRows).toBe(1);
    expect(rows.create).not.toHaveBeenCalled();
  });
});
