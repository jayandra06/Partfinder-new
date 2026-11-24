import { Injectable } from '@nestjs/common';
import type { Connection } from 'mongoose';

/** Native driver collection (avoids BSON duplicate-type friction in TS). */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type WmsCollection = any;
import { TenantMongoService } from '../../setup/tenant-mongo.service';
import { WMS_COLLECTIONS } from '../wms.types';

@Injectable()
export class WmsTenantCollectionsService {
  constructor(private readonly tenant: TenantMongoService) {}

  async connection(uri: string): Promise<Connection> {
    return this.tenant.getConnection(uri);
  }

  warehouses(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.warehouses);
  }

  stockOwners(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.stockOwners);
  }

  zones(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.zones);
  }

  locations(uri: string): Promise<WmsCollection> {
    return this.collection(uri, WMS_COLLECTIONS.locations);
  }

  async ensureIndexes(uri: string): Promise<void> {
    const wh = await this.warehouses(uri);
    const owners = await this.stockOwners(uri);
    const zones = await this.zones(uri);
    const locs = await this.locations(uri);

    await wh.createIndex({ code: 1 }, { unique: true });
    await owners.createIndex({ code: 1 }, { unique: true });
    await zones.createIndex({ warehouseId: 1, code: 1 }, { unique: true });
    await locs.createIndex({ warehouseId: 1, path: 1 }, { unique: true });
    await locs.createIndex({ warehouseId: 1, zoneId: 1 });
  }

  private async collection(uri: string, name: string): Promise<WmsCollection> {
    const conn = await this.tenant.getConnection(uri);
    const db = conn.db;
    if (!db) {
      throw new Error('Tenant database unavailable');
    }
    return db.collection(name);
  }
}