import {
  BadRequestException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import type { WmsRequestContext } from '../wms.types';
import { WMS_COLLECTIONS } from '../wms.types';
import { assertWmsAdminWrite } from '../wms-scope.util';
import { UpsertOperatorGrantDto } from '../dto/wms-operator-grant.dto';
import { WmsTenantCollectionsService } from './wms-tenant-collections.service';

@Injectable()
export class WmsOperatorGrantsService {
  constructor(private readonly collections: WmsTenantCollectionsService) {}

  async list(ctx: WmsRequestContext) {
    assertWmsAdminWrite(ctx);
    const conn = await this.collections.connection(ctx.tenantUri);
    const db = conn.db;
    if (!db) throw new BadRequestException('Tenant database unavailable.');
    const docs = await db
      .collection(WMS_COLLECTIONS.operatorGrants)
      .find({})
      .sort({ email: 1 })
      .limit(500)
      .toArray();
    return {
      items: docs.map((d) => this.mapGrant(d as Record<string, unknown>)),
      total: docs.length,
    };
  }

  async upsert(ctx: WmsRequestContext, dto: UpsertOperatorGrantDto) {
    assertWmsAdminWrite(ctx);
    const email = dto.email.trim().toLowerCase();
    if (!email.includes('@')) {
      throw new BadRequestException('Valid operator email required.');
    }
    const conn = await this.collections.connection(ctx.tenantUri);
    const db = conn.db;
    if (!db) throw new BadRequestException('Tenant database unavailable.');
    const coll = db.collection(WMS_COLLECTIONS.operatorGrants);
    const now = new Date();
    const permissions = {
      receiving: dto.permissions.receiving === true,
      qc: dto.permissions.qc === true,
      putaway: dto.permissions.putaway === true,
      supervise: dto.permissions.supervise === true,
    };
    await coll.updateOne(
      { email },
      {
        $set: {
          email,
          warehouseIds: dto.warehouseIds ?? [],
          ownerIds: dto.ownerIds ?? [],
          permissions,
          updatedAt: now,
          updatedBy: ctx.email,
        },
        $setOnInsert: { createdAt: now },
      },
      { upsert: true },
    );
    const doc = await coll.findOne({ email });
    if (!doc) throw new NotFoundException('Grant save failed.');
    return this.mapGrant(doc as Record<string, unknown>);
  }

  async remove(ctx: WmsRequestContext, email: string) {
    assertWmsAdminWrite(ctx);
    const normalized = email.trim().toLowerCase();
    const conn = await this.collections.connection(ctx.tenantUri);
    const db = conn.db;
    if (!db) throw new BadRequestException('Tenant database unavailable.');
    const res = await db.collection(WMS_COLLECTIONS.operatorGrants).deleteOne({
      email: normalized,
    });
    return { deleted: res.deletedCount === 1, email: normalized };
  }

  private mapGrant(doc: Record<string, unknown>) {
    const perms = (doc.permissions ?? {}) as Record<string, unknown>;
    return {
      email: String(doc.email ?? ''),
      warehouseIds: Array.isArray(doc.warehouseIds)
        ? doc.warehouseIds.map(String)
        : [],
      ownerIds: Array.isArray(doc.ownerIds) ? doc.ownerIds.map(String) : [],
      permissions: {
        receiving: perms.receiving === true,
        qc: perms.qc === true,
        putaway: perms.putaway === true,
        supervise: perms.supervise === true,
      },
      updatedAt: doc.updatedAt,
    };
  }
}