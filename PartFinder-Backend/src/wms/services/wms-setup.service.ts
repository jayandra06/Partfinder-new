import {
  BadRequestException,
  ConflictException,
  ForbiddenException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { Types } from 'mongoose';
import type { WmsRequestContext } from '../wms.types';
import { WmsTenantCollectionsService } from './wms-tenant-collections.service';
import { CreateWmsWarehouseDto, UpdateWmsWarehouseDto } from '../dto/wms-warehouse.dto';
import { CreateWmsStockOwnerDto, UpdateWmsStockOwnerDto } from '../dto/wms-stock-owner.dto';
import { CreateWmsZoneDto, UpdateWmsZoneDto } from '../dto/wms-zone.dto';
import { CreateWmsLocationDto, UpdateWmsLocationDto } from '../dto/wms-location.dto';
import {
  assertWarehouseAccess,
  assertOwnerAccess,
  assertWmsAdminWrite,
} from '../wms-scope.util';

function toIdString(id: unknown): string {
  if (id instanceof Types.ObjectId) {
    return id.toHexString();
  }
  return String(id ?? '');
}

function mapWarehouse(doc: Record<string, unknown>) {
  return {
    id: toIdString(doc._id),
    code: String(doc.code ?? ''),
    name: String(doc.name ?? ''),
    timeZone: String(doc.timeZone ?? 'UTC'),
    address: doc.address != null ? String(doc.address) : '',
    isActive: doc.isActive !== false,
    wmsAuthoritativeSince: doc.wmsAuthoritativeSince ?? null,
    createdAt: doc.createdAt,
    updatedAt: doc.updatedAt,
  };
}

function mapStockOwner(doc: Record<string, unknown>) {
  return {
    id: toIdString(doc._id),
    code: String(doc.code ?? ''),
    name: String(doc.name ?? ''),
    notificationEmails: Array.isArray(doc.notificationEmails)
      ? doc.notificationEmails.map(String)
      : [],
    isActive: doc.isActive !== false,
    createdAt: doc.createdAt,
    updatedAt: doc.updatedAt,
  };
}

function mapZone(doc: Record<string, unknown>) {
  return {
    id: toIdString(doc._id),
    warehouseId: String(doc.warehouseId ?? ''),
    code: String(doc.code ?? ''),
    name: String(doc.name ?? ''),
    isActive: doc.isActive !== false,
    createdAt: doc.createdAt,
    updatedAt: doc.updatedAt,
  };
}

function mapLocation(doc: Record<string, unknown>) {
  return {
    id: toIdString(doc._id),
    warehouseId: String(doc.warehouseId ?? ''),
    zoneId: doc.zoneId != null ? String(doc.zoneId) : null,
    path: String(doc.path ?? ''),
    type: String(doc.type ?? 'storage'),
    isPickable: doc.isPickable === true,
    isActive: doc.isActive !== false,
    createdAt: doc.createdAt,
    updatedAt: doc.updatedAt,
  };
}

@Injectable()
export class WmsSetupService {
  constructor(private readonly collections: WmsTenantCollectionsService) {}

  async listWarehouses(ctx: WmsRequestContext, activeOnly?: boolean) {
    await this.collections.ensureIndexes(ctx.tenantUri);
    const coll = await this.collections.warehouses(ctx.tenantUri);
    const filter: Record<string, unknown> = {};
    if (activeOnly) {
      filter.isActive = { $ne: false };
    }
    const docs = await coll.find(filter).sort({ code: 1 }).toArray();
    let items = docs.map((d: unknown) =>
      mapWarehouse(d as Record<string, unknown>),
    );
    if (ctx.allowedWarehouseIds !== 'all') {
      const allowed = new Set(ctx.allowedWarehouseIds);
      items = items.filter((w: ReturnType<typeof mapWarehouse>) =>
        allowed.has(w.id),
      );
    }
    return { items, total: items.length };
  }

  async createWarehouse(ctx: WmsRequestContext, dto: CreateWmsWarehouseDto) {
    assertWmsAdminWrite(ctx);
    await this.collections.ensureIndexes(ctx.tenantUri);
    const coll = await this.collections.warehouses(ctx.tenantUri);
    const count = await coll.countDocuments();
    if (count >= ctx.maxWmsWarehouses) {
      throw new ForbiddenException(
        `Warehouse limit reached (${ctx.maxWmsWarehouses}).`,
      );
    }
    const code = dto.code.trim().toUpperCase();
    const now = new Date();
    try {
      const result = await coll.insertOne({
        code,
        name: dto.name.trim(),
        timeZone: (dto.timeZone ?? 'UTC').trim(),
        address: dto.address?.trim() ?? '',
        isActive: dto.isActive !== false,
        wmsAuthoritativeSince: null,
        createdAt: now,
        updatedAt: now,
      });
      return mapWarehouse({
        _id: result.insertedId,
        code,
        name: dto.name.trim(),
        timeZone: dto.timeZone ?? 'UTC',
        address: dto.address ?? '',
        isActive: dto.isActive !== false,
        wmsAuthoritativeSince: null,
        createdAt: now,
        updatedAt: now,
      });
    } catch (e: unknown) {
      if (this.isDup(e)) {
        throw new ConflictException(`Warehouse code "${code}" already exists.`);
      }
      throw e;
    }
  }

  async updateWarehouse(
    ctx: WmsRequestContext,
    id: string,
    dto: UpdateWmsWarehouseDto,
  ) {
    assertWmsAdminWrite(ctx);
    assertWarehouseAccess(ctx, id);
    const coll = await this.collections.warehouses(ctx.tenantUri);
    const _id = new Types.ObjectId(id);
    const patch: Record<string, unknown> = { updatedAt: new Date() };
    if (dto.name !== undefined) {
      patch.name = dto.name.trim();
    }
    if (dto.timeZone !== undefined) {
      patch.timeZone = dto.timeZone.trim();
    }
    if (dto.address !== undefined) {
      patch.address = dto.address.trim();
    }
    if (dto.isActive !== undefined) {
      patch.isActive = dto.isActive;
    }
    const res = await coll.findOneAndUpdate(
      { _id },
      { $set: patch },
      { returnDocument: 'after' },
    );
    if (!res) {
      throw new NotFoundException('Warehouse not found.');
    }
    return mapWarehouse(res as Record<string, unknown>);
  }

  async listStockOwners(ctx: WmsRequestContext, activeOnly?: boolean) {
    await this.collections.ensureIndexes(ctx.tenantUri);
    const coll = await this.collections.stockOwners(ctx.tenantUri);
    const filter: Record<string, unknown> = {};
    if (activeOnly) {
      filter.isActive = { $ne: false };
    }
    const docs = await coll.find(filter).sort({ code: 1 }).toArray();
    let items = docs.map((d: unknown) =>
      mapStockOwner(d as Record<string, unknown>),
    );
    if (ctx.allowedOwnerIds !== 'all') {
      const allowed = new Set(ctx.allowedOwnerIds);
      items = items.filter((o: ReturnType<typeof mapStockOwner>) =>
        allowed.has(o.id),
      );
    }
    return { items, total: items.length };
  }

  async createStockOwner(ctx: WmsRequestContext, dto: CreateWmsStockOwnerDto) {
    assertWmsAdminWrite(ctx);
    const coll = await this.collections.stockOwners(ctx.tenantUri);
    const count = await coll.countDocuments();
    if (count >= ctx.maxStockOwners) {
      throw new ForbiddenException(
        `Stock owner limit reached (${ctx.maxStockOwners}).`,
      );
    }
    const code = dto.code.trim().toUpperCase();
    const now = new Date();
    try {
      const result = await coll.insertOne({
        code,
        name: dto.name.trim(),
        notificationEmails: dto.notificationEmails ?? [],
        isActive: dto.isActive !== false,
        createdAt: now,
        updatedAt: now,
      });
      return mapStockOwner({
        _id: result.insertedId,
        code,
        name: dto.name.trim(),
        notificationEmails: dto.notificationEmails ?? [],
        isActive: dto.isActive !== false,
        createdAt: now,
        updatedAt: now,
      });
    } catch (e: unknown) {
      if (this.isDup(e)) {
        throw new ConflictException(`Stock owner code "${code}" already exists.`);
      }
      throw e;
    }
  }

  async updateStockOwner(
    ctx: WmsRequestContext,
    id: string,
    dto: UpdateWmsStockOwnerDto,
  ) {
    assertWmsAdminWrite(ctx);
    assertOwnerAccess(ctx, id);
    const coll = await this.collections.stockOwners(ctx.tenantUri);
    const _id = new Types.ObjectId(id);
    const patch: Record<string, unknown> = { updatedAt: new Date() };
    if (dto.name !== undefined) {
      patch.name = dto.name.trim();
    }
    if (dto.notificationEmails !== undefined) {
      patch.notificationEmails = dto.notificationEmails;
    }
    if (dto.isActive !== undefined) {
      patch.isActive = dto.isActive;
    }
    const res = await coll.findOneAndUpdate(
      { _id },
      { $set: patch },
      { returnDocument: 'after' },
    );
    if (!res) {
      throw new NotFoundException('Stock owner not found.');
    }
    return mapStockOwner(res as Record<string, unknown>);
  }

  async listZones(ctx: WmsRequestContext, warehouseId: string, activeOnly?: boolean) {
    assertWarehouseAccess(ctx, warehouseId);
    const coll = await this.collections.zones(ctx.tenantUri);
    const filter: Record<string, unknown> = { warehouseId };
    if (activeOnly) {
      filter.isActive = { $ne: false };
    }
    const docs = await coll.find(filter).sort({ code: 1 }).toArray();
    const items = docs.map((d: unknown) =>
      mapZone(d as Record<string, unknown>),
    );
    return { items, total: items.length };
  }

  async createZone(ctx: WmsRequestContext, dto: CreateWmsZoneDto) {
    assertWmsAdminWrite(ctx);
    assertWarehouseAccess(ctx, dto.warehouseId);
    await this.assertWarehouseExists(ctx, dto.warehouseId);
    const coll = await this.collections.zones(ctx.tenantUri);
    const code = dto.code.trim().toUpperCase();
    const now = new Date();
    try {
      const result = await coll.insertOne({
        warehouseId: dto.warehouseId,
        code,
        name: dto.name.trim(),
        isActive: dto.isActive !== false,
        createdAt: now,
        updatedAt: now,
      });
      return mapZone({
        _id: result.insertedId,
        warehouseId: dto.warehouseId,
        code,
        name: dto.name.trim(),
        isActive: dto.isActive !== false,
        createdAt: now,
        updatedAt: now,
      });
    } catch (e: unknown) {
      if (this.isDup(e)) {
        throw new ConflictException(
          `Zone code "${code}" already exists in this warehouse.`,
        );
      }
      throw e;
    }
  }

  async updateZone(ctx: WmsRequestContext, id: string, dto: UpdateWmsZoneDto) {
    assertWmsAdminWrite(ctx);
    const coll = await this.collections.zones(ctx.tenantUri);
    const existing = await coll.findOne({ _id: new Types.ObjectId(id) });
    if (!existing) {
      throw new NotFoundException('Zone not found.');
    }
    assertWarehouseAccess(ctx, String(existing.warehouseId));
    const patch: Record<string, unknown> = { updatedAt: new Date() };
    if (dto.name !== undefined) {
      patch.name = dto.name.trim();
    }
    if (dto.isActive !== undefined) {
      patch.isActive = dto.isActive;
    }
    const res = await coll.findOneAndUpdate(
      { _id: new Types.ObjectId(id) },
      { $set: patch },
      { returnDocument: 'after' },
    );
    return mapZone(res as Record<string, unknown>);
  }

  async listLocations(
    ctx: WmsRequestContext,
    warehouseId: string,
    zoneId?: string,
    activeOnly?: boolean,
  ) {
    assertWarehouseAccess(ctx, warehouseId);
    const coll = await this.collections.locations(ctx.tenantUri);
    const filter: Record<string, unknown> = { warehouseId };
    if (zoneId) {
      filter.zoneId = zoneId;
    }
    if (activeOnly) {
      filter.isActive = { $ne: false };
    }
    const docs = await coll.find(filter).sort({ path: 1 }).limit(500).toArray();
    const items = docs.map((d: unknown) =>
      mapLocation(d as Record<string, unknown>),
    );
    return { items, total: items.length };
  }

  async createLocation(ctx: WmsRequestContext, dto: CreateWmsLocationDto) {
    assertWmsAdminWrite(ctx);
    assertWarehouseAccess(ctx, dto.warehouseId);
    await this.assertWarehouseExists(ctx, dto.warehouseId);
    if (dto.zoneId) {
      await this.assertZoneBelongsToWarehouse(ctx, dto.zoneId, dto.warehouseId);
    }
    const coll = await this.collections.locations(ctx.tenantUri);
    const path = dto.path.trim();
    if (!path) {
      throw new BadRequestException('Location path is required.');
    }
    const now = new Date();
    try {
      const result = await coll.insertOne({
        warehouseId: dto.warehouseId,
        zoneId: dto.zoneId ?? null,
        path,
        type: dto.type,
        isPickable: dto.isPickable === true,
        isActive: dto.isActive !== false,
        createdAt: now,
        updatedAt: now,
      });
      return mapLocation({
        _id: result.insertedId,
        warehouseId: dto.warehouseId,
        zoneId: dto.zoneId ?? null,
        path,
        type: dto.type,
        isPickable: dto.isPickable === true,
        isActive: dto.isActive !== false,
        createdAt: now,
        updatedAt: now,
      });
    } catch (e: unknown) {
      if (this.isDup(e)) {
        throw new ConflictException(
          `Location path "${path}" already exists in this warehouse.`,
        );
      }
      throw e;
    }
  }

  async updateLocation(
    ctx: WmsRequestContext,
    id: string,
    dto: UpdateWmsLocationDto,
  ) {
    assertWmsAdminWrite(ctx);
    const coll = await this.collections.locations(ctx.tenantUri);
    const existing = await coll.findOne({ _id: new Types.ObjectId(id) });
    if (!existing) {
      throw new NotFoundException('Location not found.');
    }
    const warehouseId = String(existing.warehouseId);
    assertWarehouseAccess(ctx, warehouseId);
    if (dto.zoneId) {
      await this.assertZoneBelongsToWarehouse(ctx, dto.zoneId, warehouseId);
    }
    const patch: Record<string, unknown> = { updatedAt: new Date() };
    if (dto.zoneId !== undefined) {
      patch.zoneId = dto.zoneId || null;
    }
    if (dto.path !== undefined) {
      patch.path = dto.path.trim();
    }
    if (dto.type !== undefined) {
      patch.type = dto.type;
    }
    if (dto.isPickable !== undefined) {
      patch.isPickable = dto.isPickable;
    }
    if (dto.isActive !== undefined) {
      patch.isActive = dto.isActive;
    }
    try {
      const res = await coll.findOneAndUpdate(
        { _id: new Types.ObjectId(id) },
        { $set: patch },
        { returnDocument: 'after' },
      );
      return mapLocation(res as Record<string, unknown>);
    } catch (e: unknown) {
      if (this.isDup(e)) {
        throw new ConflictException('Location path already exists in this warehouse.');
      }
      throw e;
    }
  }

  private async assertWarehouseExists(ctx: WmsRequestContext, id: string) {
    const coll = await this.collections.warehouses(ctx.tenantUri);
    const doc = await coll.findOne({
      _id: new Types.ObjectId(id),
      isActive: { $ne: false },
    });
    if (!doc) {
      throw new BadRequestException('Warehouse not found or inactive.');
    }
  }

  private async assertZoneBelongsToWarehouse(
    ctx: WmsRequestContext,
    zoneId: string,
    warehouseId: string,
  ) {
    const coll = await this.collections.zones(ctx.tenantUri);
    const doc = await coll.findOne({ _id: new Types.ObjectId(zoneId) });
    if (!doc || String(doc.warehouseId) !== warehouseId) {
      throw new BadRequestException('Zone does not belong to the selected warehouse.');
    }
  }

  private isDup(e: unknown): boolean {
    return (
      !!e &&
      typeof e === 'object' &&
      'code' in e &&
      (e as { code?: number }).code === 11000
    );
  }
}