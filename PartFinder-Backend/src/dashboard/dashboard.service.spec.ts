import { Logger } from '@nestjs/common';
import { RedisService } from '../common/redis/redis.service';
import { DashboardService } from './dashboard.service';

function setup(totalParts = 100, templates = 4, lowStock: Array<{ n: number }> = [{ n: 7 }]) {
  const parts = {
    countDocuments: jest.fn().mockResolvedValue(totalParts),
    aggregate: jest.fn().mockReturnValue({ exec: jest.fn().mockResolvedValue(lowStock) }),
  };
  const tpl = { countDocuments: jest.fn().mockResolvedValue(templates) };
  const redis = new RedisService({ get: () => undefined } as any);
  const service = new DashboardService(parts as any, tpl as any, redis);
  return { service, parts, tpl, redis };
}

describe('DashboardService', () => {
  beforeEach(() => {
    jest.spyOn(console, 'log').mockImplementation(() => undefined);
    jest.spyOn(console, 'warn').mockImplementation(() => undefined);
    jest.spyOn(Logger.prototype, 'warn').mockImplementation(() => undefined);
  });
  afterEach(() => jest.restoreAllMocks());

  it('computes stats scoped to the organisation', async () => {
    const { service, parts, tpl } = setup();

    const stats = await service.stats('org1');

    expect(stats).toMatchObject({ totalParts: 100, activeTemplates: 4, lowStock: 7 });
    expect(parts.countDocuments).toHaveBeenCalledWith({ orgId: 'org1' });
    expect(tpl.countDocuments).toHaveBeenCalledWith({ orgId: 'org1' });
    const pipeline = parts.aggregate.mock.calls[0][0];
    expect(pipeline[0]).toEqual({ $match: { orgId: 'org1' } });
  });

  it('reports zero low stock when the aggregation is empty', async () => {
    const { service } = setup(5, 1, []);

    await expect(service.stats('org1')).resolves.toMatchObject({ lowStock: 0 });
  });

  it('serves repeated stats from cache', async () => {
    const { service, parts } = setup();

    await service.stats('org1');
    await service.stats('org1');

    expect(parts.countDocuments).toHaveBeenCalledTimes(1);
  });

  it('caches per organisation', async () => {
    const { service, parts } = setup();

    await service.stats('org1');
    await service.stats('org2');

    expect(parts.countDocuments).toHaveBeenCalledTimes(2);
  });

  it('builds a 12-point trend that grows from 70% of the part count', async () => {
    const { service } = setup(100);

    const trend = await service.trend('org1');

    expect(trend).toHaveLength(12);
    expect(trend[0]).toEqual({ label: 'M1', value: 70 });
    expect(trend[11]).toEqual({ label: 'M12', value: 103 });
  });

  it('caches the trend', async () => {
    const { service, parts } = setup(10);

    await service.trend('org1');
    await service.trend('org1');

    expect(parts.countDocuments).toHaveBeenCalledTimes(1);
  });
});
