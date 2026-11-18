import {
  BadRequestException,
  ConflictException,
  Injectable,
} from '@nestjs/common';
import { ulid } from 'ulid';
import type { WmsRequestContext } from '../wms.types';
import { assertWmsAdminWrite } from '../wms-scope.util';
import { CreateWmsErpConnectorDto } from '../dto/wms-sku.dto';
import {
  WmsErpLocationMappingDto,
  WmsErpSkuMappingDto,
} from '../dto/wms-inventory.dto';
import { WmsTenantCollectionsService } from './wms-tenant-collections.service';

@Injectable()
export class WmsErpFoundationService {
  constructor(private readonly collections: WmsTenantCollectionsService) {}

  async listConnectors(ctx: WmsRequestContext) {
    await this.collections.ensurePhase02Indexes(ctx.tenantUri);
    const coll = await this.collections.erpConnectors(ctx.tenantUri);
    const docs = await coll.find({}).sort({ name: 1 }).toArray();
    return docs.map((d: Record<string, unknown>) => ({
      erpConnectorId: String(d.erpConnectorId),
      name: String(d.name ?? ''),
      connectorType: String(d.connectorType ?? 'manual'),
      ownerId: d.ownerId ?? null,
      isActive: d.isActive !== false,
      syncStatus: String(d.syncStatus ?? 'healthy'),
      createdAt: d.createdAt,
    }));
  }

  async createConnector(ctx: WmsRequestContext, dto: CreateWmsErpConnectorDto) {
    assertWmsAdminWrite(ctx);
    await this.collections.ensurePhase02Indexes(ctx.tenantUri);
    const coll = await this.collections.erpConnectors(ctx.tenantUri);
    const erpConnectorId = ulid();
    const now = new Date();
    const doc = {
      erpConnectorId,
      name: dto.name.trim(),
      connectorType: dto.connectorType,
      ownerId: dto.ownerId?.trim() || null,
      isActive: true,
      syncStatus: 'healthy',
      lastInboundAt: null,
      lastOutboundAt: null,
      lastError: null,
      createdAt: now,
      updatedAt: now,
    };
    try {
      await coll.insertOne(doc);
    } catch (e: unknown) {
      if (this.isDup(e)) {
        throw new ConflictException('Connector conflict.');
      }
      throw e;
    }
    return doc;
  }

  async listSkuMappings(ctx: WmsRequestContext, erpConnectorId?: string) {
    await this.collections.ensurePhase02B1Indexes(ctx.tenantUri);
    const coll = await this.collections.erpSkuMappings(ctx.tenantUri);
    const filter = erpConnectorId ? { erpConnectorId } : {};
    const docs = await coll.find(filter).sort({ externalMaterialId: 1 }).toArray();
    return docs.map((d: Record<string, unknown>) => ({
      erpConnectorId: String(d.erpConnectorId),
      externalMaterialId: String(d.externalMaterialId),
      skuId: String(d.skuId),
      updatedAt: d.updatedAt,
    }));
  }

  async upsertSkuMapping(ctx: WmsRequestContext, dto: WmsErpSkuMappingDto) {
    assertWmsAdminWrite(ctx);
    await this.collections.ensurePhase02B1Indexes(ctx.tenantUri);
    const coll = await this.collections.erpSkuMappings(ctx.tenantUri);
    const now = new Date();
    await coll.updateOne(
      {
        erpConnectorId: dto.erpConnectorId,
        externalMaterialId: dto.externalMaterialId.trim(),
      },
      {
        $set: {
          skuId: dto.skuId.trim(),
          updatedAt: now,
        },
        $setOnInsert: { createdAt: now },
      },
      { upsert: true },
    );
    return {
      erpConnectorId: dto.erpConnectorId,
      externalMaterialId: dto.externalMaterialId.trim(),
      skuId: dto.skuId.trim(),
    };
  }

  async listLocationMappings(ctx: WmsRequestContext, erpConnectorId?: string) {
    await this.collections.ensurePhase02B1Indexes(ctx.tenantUri);
    const coll = await this.collections.erpLocationMappings(ctx.tenantUri);
    const filter = erpConnectorId ? { erpConnectorId } : {};
    const docs = await coll.find(filter).sort({ externalLocationId: 1 }).toArray();
    return docs.map((d: Record<string, unknown>) => ({
      erpConnectorId: String(d.erpConnectorId),
      externalLocationId: String(d.externalLocationId),
      warehouseId: String(d.warehouseId),
      locationId: String(d.locationId),
      updatedAt: d.updatedAt,
    }));
  }

  async upsertLocationMapping(ctx: WmsRequestContext, dto: WmsErpLocationMappingDto) {
    assertWmsAdminWrite(ctx);
    await this.collections.ensurePhase02B1Indexes(ctx.tenantUri);
    const coll = await this.collections.erpLocationMappings(ctx.tenantUri);
    const now = new Date();
    await coll.updateOne(
      {
        erpConnectorId: dto.erpConnectorId,
        externalLocationId: dto.externalLocationId.trim(),
      },
      {
        $set: {
          warehouseId: dto.warehouseId,
          locationId: dto.locationId,
          updatedAt: now,
        },
        $setOnInsert: { createdAt: now },
      },
      { upsert: true },
    );
    return { ...dto };
  }

  async listOutbox(ctx: WmsRequestContext, status?: string) {
    await this.collections.ensurePhase02B1Indexes(ctx.tenantUri);
    const coll = await this.collections.outboxEvents(ctx.tenantUri);
    const filter = status ? { status } : {};
    const docs = await coll.find(filter).sort({ createdAt: -1 }).limit(200).toArray();
    return docs.map((d: Record<string, unknown>) => ({
      eventId: String(d.eventId),
      txnId: String(d.txnId),
      eventType: String(d.eventType),
      status: String(d.status),
      attemptCount: Number(d.attemptCount ?? 0),
      lastError: d.lastError ?? null,
      createdAt: d.createdAt,
    }));
  }

  async recordConnectorError(
    ctx: WmsRequestContext,
    erpConnectorId: string,
    error: string,
  ) {
    assertWmsAdminWrite(ctx);
    const coll = await this.collections.erpConnectors(ctx.tenantUri);
    await coll.updateOne(
      { erpConnectorId },
      {
        $set: {
          syncStatus: 'error',
          lastError: error.slice(0, 2000),
          updatedAt: new Date(),
        },
      },
    );
  }

  async registerInboundMessageStub(
    ctx: WmsRequestContext,
    erpConnectorId: string,
    idempotencyKey: string,
    messageType: string,
    externalDocumentId?: string,
  ) {
    assertWmsAdminWrite(ctx);
    if (!idempotencyKey.trim()) {
      throw new BadRequestException('idempotencyKey required.');
    }
    const coll = await this.collections.inboundMessages(ctx.tenantUri);
    const existing = await coll.findOne({
      erpConnectorId,
      idempotencyKey: idempotencyKey.trim(),
    });
    if (existing) {
      return {
        status: String(existing.status),
        duplicate: true,
        note: 'No stock mutation — validate-only stub.',
      };
    }
    const now = new Date();
    await coll.insertOne({
      erpConnectorId,
      idempotencyKey: idempotencyKey.trim(),
      messageType,
      externalDocumentId: externalDocumentId?.trim() || null,
      status: 'received',
      createdAt: now,
      updatedAt: now,
    });
    return {
      status: 'received',
      duplicate: false,
      note: 'No stock mutation — validate-only stub.',
    };
  }

  private isDup(e: unknown): boolean {
    return (
      typeof e === 'object' &&
      e !== null &&
      'code' in e &&
      (e as { code?: number }).code === 11000
    );
  }
}