import { Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { RedisService } from './redis.service';

function inMemoryRedis(): RedisService {
  // No REDIS_URL -> the service must fall back to its in-memory store.
  const config = { get: jest.fn().mockReturnValue(undefined) } as unknown as ConfigService;
  return new RedisService(config);
}

describe('RedisService (in-memory fallback)', () => {
  beforeAll(() => {
    jest.spyOn(Logger.prototype, 'warn').mockImplementation(() => undefined);
  });

  afterAll(() => {
    jest.restoreAllMocks();
  });

  it('round-trips JSON values', async () => {
    const redis = inMemoryRedis();
    await redis.setJson('k', { a: 1, list: ['x'] }, 30);

    await expect(redis.getJson('k')).resolves.toEqual({ a: 1, list: ['x'] });
  });

  it('returns null for missing keys', async () => {
    const redis = inMemoryRedis();

    await expect(redis.getJson('missing')).resolves.toBeNull();
    await expect(redis.getString('missing')).resolves.toBeNull();
  });

  it('drops corrupt JSON and returns null', async () => {
    const redis = inMemoryRedis();
    await redis.setString('bad', '{not json');

    await expect(redis.getJson('bad')).resolves.toBeNull();
    await expect(redis.exists('bad')).resolves.toBe(false);
  });

  it('deletes single keys', async () => {
    const redis = inMemoryRedis();
    await redis.setString('a', '1');
    await redis.delete('a');

    await expect(redis.exists('a')).resolves.toBe(false);
  });

  it('deletes only keys with the given prefix and reports the count', async () => {
    const redis = inMemoryRedis();
    await redis.setString('pf:org1:a', '1');
    await redis.setString('pf:org1:b', '2');
    await redis.setString('pf:org2:a', '3');

    await expect(redis.deleteByPrefix('pf:org1:')).resolves.toBe(2);
    await expect(redis.exists('pf:org1:a')).resolves.toBe(false);
    await expect(redis.exists('pf:org2:a')).resolves.toBe(true);
  });

  it('only reads REDIS_URL once even when it is missing', async () => {
    const get = jest.fn().mockReturnValue('  ');
    const redis = new RedisService({ get } as unknown as ConfigService);

    await redis.setString('a', '1');
    await redis.getString('a');
    await redis.exists('a');

    expect(get).toHaveBeenCalledTimes(1);
    expect(get).toHaveBeenCalledWith('REDIS_URL');
  });

  it('onModuleDestroy is safe without a client', async () => {
    const redis = inMemoryRedis();

    await expect(redis.onModuleDestroy()).resolves.toBeUndefined();
  });
});
