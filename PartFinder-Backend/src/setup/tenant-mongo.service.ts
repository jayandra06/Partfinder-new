import { Injectable, OnModuleDestroy } from '@nestjs/common';
import type { Connection } from 'mongoose';
import mongoose from 'mongoose';

export const ORG_ADMIN_COLLECTION = 'org_admin_users';
const SETUP_META_COLLECTION = '_partfinder_setup';

function requireDb(conn: Connection) {
  const db = conn.db;
  if (!db) {
    throw new Error('Mongo connection has no database');
  }
  return db;
}

@Injectable()
export class TenantMongoService implements OnModuleDestroy {
  private readonly connections = new Map<string, Connection>();

  async onModuleDestroy() {
    for (const c of this.connections.values()) {
      await c.close().catch(() => undefined);
    }
    this.connections.clear();
  }

  async pingUri(uri: string): Promise<void> {
    const c = mongoose.createConnection(uri);
    try {
      await c.asPromise();
      await c.db?.admin().command({ ping: 1 });
    } finally {
      await c.close().catch(() => undefined);
    }
  }

  async getConnection(uri: string): Promise<Connection> {
    let c = this.connections.get(uri);
    if (!c || c.readyState !== 1) {
      if (c) {
        await c.close().catch(() => undefined);
      }
      c = mongoose.createConnection(uri);
      await c.asPromise();
      this.connections.set(uri, c);
    }
    return c;
  }

  orgAdminCollection(conn: Connection) {
    return requireDb(conn).collection(ORG_ADMIN_COLLECTION);
  }

  async initializeTenantDatabase(uri: string): Promise<void> {
    const c = await this.getConnection(uri);
    const db = requireDb(c);
    const admins = this.orgAdminCollection(c);
    await admins.createIndex({ email: 1 }, { unique: true });
    const meta = db.collection<{ _id: string; bootstrappedAt?: Date; version?: number }>(
      SETUP_META_COLLECTION,
    );
    await meta.updateOne(
      { _id: 'bootstrap' },
      { $setOnInsert: { bootstrappedAt: new Date(), version: 1 } },
      { upsert: true },
    );
    await db.admin().command({ ping: 1 });
  }

  async countOrgAdmins(uri: string): Promise<number> {
    const c = await this.getConnection(uri);
    return this.orgAdminCollection(c).countDocuments();
  }

  async hasAnyOrgAdmin(uri: string): Promise<boolean> {
    const n = await this.countOrgAdmins(uri);
    return n > 0;
  }

  async createOrgAdminIfNone(
    uri: string,
    name: string,
    email: string,
    passwordHash: string,
  ): Promise<{ created: boolean; skipped: boolean }> {
    const c = await this.getConnection(uri);
    const coll = this.orgAdminCollection(c);
    const existing = await coll.findOne({});
    if (existing) {
      return { created: false, skipped: true };
    }
    try {
      await coll.insertOne({
        email: email.toLowerCase().trim(),
        passwordHash,
        name: name.trim(),
        createdAt: new Date(),
      });
      return { created: true, skipped: false };
    } catch (e: unknown) {
      const dup =
        e &&
        typeof e === 'object' &&
        'code' in e &&
        (e as { code?: number }).code === 11000;
      if (dup) {
        return { created: false, skipped: true };
      }
      throw e;
    }
  }
}