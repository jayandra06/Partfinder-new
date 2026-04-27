import { Injectable, Logger, OnModuleDestroy } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { createClient, RedisClientType } from 'redis';

@Injectable()
export class RedisService implements OnModuleDestroy {
  private readonly logger = new Logger(RedisService.name);
  private readonly memory = new Map<string, string>();
  private client: RedisClientType | null = null;
  private ready = false;
  private attempted = false;

  constructor(private readonly config: ConfigService) {}

  async getJson<T>(key: string): Promise<T | null> {
    const raw = await this.getString(key);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as T;
    } catch {
      await this.delete(key);
      return null;
    }
  }

  async setJson(key: string, value: unknown, ttlSeconds?: number): Promise<void> {
    await this.setString(key, JSON.stringify(value), ttlSeconds);
  }

  async getString(key: string): Promise<string | null> {
    const client = await this.getClient();
    if (!client) {
      return this.memory.get(key) ?? null;
    }
    return await client.get(key);
  }

  async setString(key: string, value: string, ttlSeconds?: number): Promise<void> {
    const client = await this.getClient();
    if (!client) {
      this.memory.set(key, value);
      return;
    }

    if (ttlSeconds && ttlSeconds > 0) {
      await client.set(key, value, { EX: ttlSeconds });
      return;
    }

    await client.set(key, value);
  }

  async delete(key: string): Promise<void> {
    const client = await this.getClient();
    if (!client) {
      this.memory.delete(key);
      return;
    }
    await client.del(key);
  }

  async onModuleDestroy(): Promise<void> {
    if (this.client) {
      await this.client.quit();
    }
    this.ready = false;
    this.client = null;
  }

  private async getClient(): Promise<RedisClientType | null> {
    if (this.ready && this.client) {
      return this.client;
    }

    if (this.attempted) {
      return null;
    }

    this.attempted = true;
    const redisUrl = this.config.get<string>('REDIS_URL')?.trim();
    if (!redisUrl) {
      this.logger.warn('REDIS_URL is not set. Falling back to in-memory cache/state.');
      return null;
    }

    try {
      this.client = createClient({ url: redisUrl });
      this.client.on('error', (err) => {
        this.logger.warn(`Redis connection issue: ${err instanceof Error ? err.message : String(err)}`);
      });
      await this.client.connect();
      this.ready = true;
      this.logger.log('Connected to Redis.');
      return this.client;
    } catch (error) {
      this.logger.warn(
        `Failed to connect Redis. Falling back to in-memory cache/state. ${error instanceof Error ? error.message : String(error)}`,
      );
      this.client = null;
      this.ready = false;
      return null;
    }
  }
}
