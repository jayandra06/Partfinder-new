import {
  BadRequestException,
  Injectable,
  InternalServerErrorException,
  NotFoundException,
} from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import mongoose, { Model } from 'mongoose';
import { createMongooseConnectionWithSrvDnsRetry } from '../mongo-srv-dns-retry';
import {
  assertMongoUri,
  prepareMongoUriForDriver,
} from '../mongo-uri';
import {
  Organization,
  OrganizationDocument,
} from '../organizations/schemas/organization.schema';
import { DbCluster, DbClusterDocument } from './schemas/db-cluster.schema';

const DEFAULT_MAX_DATABASES_PER_CLUSTER = 250;

function maskMongoUri(uri: string): string {
  const t = uri.trim();
  if (t.length <= 24) {
    return '****';
  }
  return `${t.slice(0, 16)}...${t.slice(-8)}`;
}

@Injectable()
export class DbClustersService {
  constructor(
    @InjectModel(DbCluster.name)
    private readonly clusterModel: Model<DbClusterDocument>,
    @InjectModel(Organization.name)
    private readonly orgModel: Model<OrganizationDocument>,
  ) {}

  /**
   * Choose a registered cluster with room for another tenant DB (least loaded first).
   */
  async pickClusterForProvisioning(): Promise<{
    connectionUri: string;
    clusterId: mongoose.Types.ObjectId;
  } | null> {
    const clusters = await this.clusterModel.find().sort({ createdAt: 1 }).lean();
    type Best = {
      clusterId: mongoose.Types.ObjectId;
      connectionUri: string;
      used: number;
    };
    let best: Best | null = null;
    for (const c of clusters) {
      const max =
        (c as { maxDatabases?: number }).maxDatabases ??
        DEFAULT_MAX_DATABASES_PER_CLUSTER;
      const used = await this.orgModel.countDocuments({
        assignedDbClusterId: c._id,
      });
      if (used >= max) {
        continue;
      }
      if (!best || used < best.used) {
        best = {
          clusterId: c._id as mongoose.Types.ObjectId,
          connectionUri: String(c.connectionUri),
          used,
        };
      }
    }
    if (!best) {
      return null;
    }
    return {
      connectionUri: best.connectionUri,
      clusterId: best.clusterId,
    };
  }

  /**
   * Ping MongoDB without throwing — used by POST …/test so connection failures are not HTTP 400.
   */
  async probeConnection(
    rawUri: string,
  ): Promise<{ ok: true } | { ok: false; message: string }> {
    const uri = prepareMongoUriForDriver(rawUri);
    try {
      assertMongoUri(uri);
    } catch {
      return {
        ok: false,
        message: 'Invalid MongoDB URI. Use mongodb:// or mongodb+srv://',
      };
    }
    let c: mongoose.Connection | undefined;
    try {
      c = await createMongooseConnectionWithSrvDnsRetry(uri);
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
          ' SRV DNS failed (default resolver and 1.1.1.1/8.8.8.8). Use Atlas "Drivers" -> standard connection string (mongodb://host:27017,...), or fix VPN/DNS/firewall.';
      }
      return { ok: false, message };
    } finally {
      await c?.close().catch(() => undefined);
    }
  }

  async create(rawUri: string): Promise<{ id: string; uriMasked: string }> {
    const uri = prepareMongoUriForDriver(rawUri);
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
    {
      id: string;
      uriMasked: string;
      createdAt: string;
      databasesUsed: number;
      maxDatabases: number;
      databasesFree: number;
    }[]
  > {
    const rows = await this.clusterModel
      .find()
      .sort({ createdAt: -1 })
      .lean()
      .exec();
    const out: {
      id: string;
      uriMasked: string;
      createdAt: string;
      databasesUsed: number;
      maxDatabases: number;
      databasesFree: number;
    }[] = [];
    for (const r of rows) {
      const created = (r as { createdAt?: Date }).createdAt;
      const max =
        (r as { maxDatabases?: number }).maxDatabases ??
        DEFAULT_MAX_DATABASES_PER_CLUSTER;
      const used = await this.orgModel.countDocuments({
        assignedDbClusterId: r._id,
      });
      out.push({
        id: String(r._id),
        uriMasked: maskMongoUri(String(r.connectionUri)),
        createdAt:
          created instanceof Date ? created.toISOString() : new Date().toISOString(),
        databasesUsed: used,
        maxDatabases: max,
        databasesFree: Math.max(0, max - used),
      });
    }
    return out;
  }

  /**
   * Organizations provisioned on this cluster (Default DB mode).
   */
  async findOrganizationsForCluster(clusterId: string): Promise<
    {
      id: string;
      orgCode: string;
      name: string;
      status: string;
      createdAt: string;
    }[]
  > {
    if (!mongoose.Types.ObjectId.isValid(clusterId)) {
      throw new BadRequestException('Invalid cluster id');
    }
    const oid = new mongoose.Types.ObjectId(clusterId);
    const cluster = await this.clusterModel.findById(oid).lean().exec();
    if (!cluster) {
      throw new NotFoundException('Cluster not found');
    }
    const orgs = await this.orgModel
      .find({ assignedDbClusterId: oid })
      .sort({ orgCode: 1 })
      .select({ orgCode: 1, name: 1, status: 1, createdAt: 1 })
      .lean()
      .exec();
    return orgs.map((o) => {
      const created = (o as { createdAt?: Date }).createdAt;
      return {
        id: String(o._id),
        orgCode: String(o.orgCode),
        name: String(o.name),
        status: String((o as { status?: string }).status ?? ''),
        createdAt:
          created instanceof Date ? created.toISOString() : new Date().toISOString(),
      };
    });
  }
}
