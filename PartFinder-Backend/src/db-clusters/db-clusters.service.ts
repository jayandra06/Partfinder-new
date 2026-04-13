import {
  BadRequestException,
  Injectable,
  InternalServerErrorException,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import mongoose, { Model } from 'mongoose';
import { assertMongoUri, normalizeMongoUri } from '../mongo-uri';
import { DbCluster, DbClusterDocument } from './schemas/db-cluster.schema';

function maskMongoUri(uri: string): string {
  const t = uri.trim();
  if (t.length <= 24) {
    return '••••';
  }
  return `${t.slice(0, 16)}…${t.slice(-8)}`;
}

@Injectable()
export class DbClustersService {
  constructor(
    @InjectModel(DbCluster.name)
    private readonly clusterModel: Model<DbClusterDocument>,
  ) {}

  /**
   * Ping MongoDB without throwing — used by POST …/test so connection failures are not HTTP 400.
   */
  async probeConnection(
    rawUri: string,
  ): Promise<{ ok: true } | { ok: false; message: string }> {
    const uri = normalizeMongoUri(rawUri);
    try {
      assertMongoUri(uri);
    } catch {
      return {
        ok: false,
        message: 'Invalid MongoDB URI. Use mongodb:// or mongodb+srv://',
      };
    }
    const c = mongoose.createConnection(uri);
    try {
      await c.asPromise();
      const db = c.db;
      if (!db) {
        return {
          ok: false,
          message:
            'Connected but could not access the database handle. Check the URI path and options.',
        };
      }
      await db.admin().command({ ping: 1 });
      return { ok: true as const };
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Connection failed';
      let message = `Could not connect: ${msg}`;
      if (/querySrv|ECONNREFUSED|ENOTFOUND/i.test(msg)) {
        message +=
          ' SRV DNS lookup failed — in Atlas use “Drivers” → standard connection string (mongodb://…:27017,…), or check VPN/DNS/firewall.';
      }
      return { ok: false, message };
    } finally {
      await c.close().catch(() => undefined);
    }
  }

  async create(rawUri: string): Promise<{ id: string; uriMasked: string }> {
    const uri = normalizeMongoUri(rawUri);
    try {
      assertMongoUri(uri);
    } catch {
      throw new BadRequestException(
        'Invalid MongoDB URI. Use mongodb:// or mongodb+srv://',
      );
    }
    const probe = await this.probeConnection(uri);
    if (!probe.ok) {
      throw new BadRequestException(probe.message);
    }
    try {
      const doc = await this.clusterModel.create({ connectionUri: uri });
      return { id: String(doc._id), uriMasked: maskMongoUri(uri) };
    } catch {
      throw new InternalServerErrorException('Failed to save cluster');
    }
  }

  async findAllForAdmin(): Promise<
    { id: string; uriMasked: string; createdAt: string }[]
  > {
    const rows = await this.clusterModel
      .find()
      .sort({ createdAt: -1 })
      .lean()
      .exec();
    return rows.map((r) => {
      const created = (r as { createdAt?: Date }).createdAt;
      return {
        id: String(r._id),
        uriMasked: maskMongoUri(String(r.connectionUri)),
        createdAt:
          created instanceof Date ? created.toISOString() : new Date().toISOString(),
      };
    });
  }
}
