import {
  BadRequestException,
  ConflictException,
  ForbiddenException,
  Injectable,
  NotFoundException,
} from '@nestjs/common';
import { ulid } from 'ulid';
import type { WmsRequestContext, WmsSkuStatus } from '../wms.types';
import {
  identifierScopeKey,
  parseNamespace,
  type WmsSkuNamespace,
} from '../wms-sku-namespaces';
import { assertWmsAdminWrite } from '../wms-scope.util';
import {
  AddWmsSkuIdentifierDto,
  CreateWmsSkuDto,
  MergeWmsSkuDto,
  RetireWmsSkuDto,
  UpdateWmsSkuDto,
} from '../dto/wms-sku.dto';
import { WmsSkuAuditService } from './wms-sku-audit.service';
import { WmsTenantCollectionsService } from './wms-tenant-collections.service';
import { WmsUomService } from './wms-uom.service';
import { WmsInventoryReadService } from './wms-inventory-read.service';

@Injectable()
export class WmsSkuService {
  constructor(
    private readonly collections: WmsTenantCollectionsService,
    private readonly audit: WmsSkuAuditService,
    private readonly uom: WmsUomService,
    private readonly inventoryRead: WmsInventoryReadService,
  ) {}

  async listSkus(ctx: WmsRequestContext, status?: WmsSkuStatus) {
    await this.collections.ensurePhase02Indexes(ctx.tenantUri);
    const coll = await this.collections.skus(ctx.tenantUri);
    const filter = status ? { status } : {};
    const docs = await coll.find(filter).sort({ displayName: 1 }).toArray();
    return { items: docs.map((d: Record<string, unknown>) => this.mapSku(d)), total: docs.length };
  }

  async getSku(ctx: WmsRequestContext, skuId: string) {
    const sku = await this.findSku(ctx, skuId);
    const identifiers = await this.listIdentifiers(ctx, skuId);
    return { ...this.mapSku(sku), identifiers };
  }

  async createSku(ctx: WmsRequestContext, dto: CreateWmsSkuDto) {
    assertWmsAdminWrite(ctx);
    await this.uom.assertUomExists(ctx, dto.primaryUom);
    await this.collections.ensurePhase02Indexes(ctx.tenantUri);
    const coll = await this.collections.skus(ctx.tenantUri);
    const skuId = ulid();
    const now = new Date();
    const doc = {
      skuId,
      displayName: dto.displayName.trim(),
      description: dto.description?.trim() ?? '',
      primaryUom: dto.primaryUom.trim().toUpperCase(),
      variantGroupId: dto.variantGroupId?.trim() || null,
      variantRevision: dto.variantRevision?.trim() || null,
      dimensions: dto.dimensions ?? {},
      packaging: dto.packaging ?? {},
      status: 'active' as const,
      mergedIntoSkuId: null,
      createdAt: now,
      updatedAt: now,
    };
    await coll.insertOne(doc);
    await this.audit.append(ctx, 'sku.created', skuId, {
      displayName: doc.displayName,
      primaryUom: doc.primaryUom,
    });
    return this.mapSku(doc);
  }

  async updateSku(ctx: WmsRequestContext, skuId: string, dto: UpdateWmsSkuDto) {
    assertWmsAdminWrite(ctx);
    const existing = await this.findSku(ctx, skuId);
    if (existing.status !== 'active') {
      throw new ForbiddenException('Cannot update a merged or retired SKU.');
    }
    if (dto.primaryUom) {
      await this.uom.assertUomExists(ctx, dto.primaryUom);
    }
    const coll = await this.collections.skus(ctx.tenantUri);
    const patch: Record<string, unknown> = { updatedAt: new Date() };
    if (dto.displayName !== undefined) patch.displayName = dto.displayName.trim();
    if (dto.description !== undefined) patch.description = dto.description.trim();
    if (dto.primaryUom !== undefined)
      patch.primaryUom = dto.primaryUom.trim().toUpperCase();
    if (dto.variantGroupId !== undefined)
      patch.variantGroupId = dto.variantGroupId?.trim() || null;
    if (dto.variantRevision !== undefined)
      patch.variantRevision = dto.variantRevision?.trim() || null;
    if (dto.dimensions !== undefined) patch.dimensions = dto.dimensions;
    if (dto.packaging !== undefined) patch.packaging = dto.packaging;

    const res = await coll.findOneAndUpdate(
      { skuId },
      { $set: patch },
      { returnDocument: 'after' },
    );
    if (!res) {
      throw new NotFoundException('SKU not found.');
    }
    await this.audit.append(ctx, 'sku.updated', skuId, { fields: Object.keys(patch) });
    return this.mapSku(res as Record<string, unknown>);
  }

  async addIdentifier(
    ctx: WmsRequestContext,
    skuId: string,
    dto: AddWmsSkuIdentifierDto,
  ) {
    assertWmsAdminWrite(ctx);
    const namespace = parseNamespace(dto.namespace);
    if (!namespace) {
      throw new BadRequestException('Invalid identifier namespace.');
    }
    const sku = await this.findSku(ctx, skuId);
    if (sku.status !== 'active') {
      throw new ForbiddenException('Cannot add identifiers to inactive SKU.');
    }

    const metadata = { ...(dto.metadata ?? {}) };
    const scopeKey = identifierScopeKey(namespace, dto.value, metadata);
    const conflict = await this.checkScopeConflict(ctx, scopeKey, skuId);
    if (conflict) {
      throw new ConflictException(this.sanitizeConflictBody(ctx, conflict, scopeKey));
    }

    const coll = await this.collections.skuIdentifiers(ctx.tenantUri);
    const now = new Date();
    const doc = {
      skuId,
      namespace,
      value: dto.value.trim(),
      metadata,
      scopeKey,
      status: 'active' as const,
      createdAt: now,
      updatedAt: now,
    };
    try {
      await coll.insertOne(doc);
    } catch (e: unknown) {
      if (this.isDup(e)) {
        throw new ConflictException('Identifier scope already registered.');
      }
      throw e;
    }

    await this.audit.append(ctx, 'sku.identifier.added', skuId, {
      namespace,
      scopeKey,
    });
    return doc;
  }

  async checkIdentifier(
    ctx: WmsRequestContext,
    dto: AddWmsSkuIdentifierDto,
    excludeSkuId?: string,
  ) {
    const namespace = parseNamespace(dto.namespace);
    if (!namespace) {
      throw new BadRequestException('Invalid identifier namespace.');
    }
    const metadata = { ...(dto.metadata ?? {}) };
    const scopeKey = identifierScopeKey(namespace, dto.value, metadata);
    const existing = await this.checkScopeConflict(ctx, scopeKey, excludeSkuId);
    const ambiguous = await this.findAmbiguousMatches(
      ctx,
      namespace,
      dto.value,
      metadata,
    );
    return {
      scopeKey,
      conflict: existing
        ? this.sanitizeConflictBody(ctx, existing, scopeKey)
        : null,
      ambiguousCandidates: ctx.isWmsAdmin
        ? ambiguous
        : ambiguous.map((a: { scopeKey: string }) => ({ scopeKey: a.scopeKey })),
    };
  }

  async mergeSku(ctx: WmsRequestContext, skuId: string, dto: MergeWmsSkuDto) {
    assertWmsAdminWrite(ctx);
    const source = await this.findSku(ctx, skuId);
    if (source.status !== 'active') {
      throw new BadRequestException('SKU is not active.');
    }
    if (skuId === dto.mergedIntoSkuId) {
      throw new BadRequestException('Cannot merge SKU into itself.');
    }
    const target = await this.findSku(ctx, dto.mergedIntoSkuId);
    if (target.status !== 'active') {
      throw new BadRequestException('Target SKU must be active.');
    }
    if (String(source.variantGroupId ?? '') && String(target.variantGroupId ?? '')) {
      if (source.variantGroupId !== target.variantGroupId) {
        throw new BadRequestException(
          'Cannot merge SKUs from different variant groups without manual review.',
        );
      }
    }
    const hasStock = await this.inventoryRead.hasStockForSku(ctx, skuId);
    if (hasStock) {
      throw new BadRequestException(
        'SKU has on-hand stock. Transfer or adjust stock before merge.',
      );
    }
    const txnRefs = await this.inventoryRead.countTxnReferences(ctx, skuId);
    if (txnRefs > 0 && !dto.reason?.trim()) {
      throw new BadRequestException(
        'SKU has ledger history. Provide merge reason for audit.',
      );
    }

    const coll = await this.collections.skus(ctx.tenantUri);
    const now = new Date();
    await coll.updateOne(
      { skuId },
      {
        $set: {
          status: 'merged',
          mergedIntoSkuId: dto.mergedIntoSkuId,
          updatedAt: now,
        },
      },
    );

    const idColl = await this.collections.skuIdentifiers(ctx.tenantUri);
    await idColl.updateMany(
      { skuId, status: 'active' },
      { $set: { status: 'superseded', updatedAt: now } },
    );

    await this.audit.append(ctx, 'sku.merged', skuId, {
      mergedIntoSkuId: dto.mergedIntoSkuId,
      reason: dto.reason?.trim() ?? '',
      note: 'Historical ledger lines are not rewritten.',
    });
    return { skuId, mergedIntoSkuId: dto.mergedIntoSkuId, status: 'merged' };
  }

  async retireSku(ctx: WmsRequestContext, skuId: string, dto: RetireWmsSkuDto) {
    assertWmsAdminWrite(ctx);
    const source = await this.findSku(ctx, skuId);
    if (source.status === 'retired') {
      return { skuId, status: 'retired' };
    }
    const hasStock = await this.inventoryRead.hasStockForSku(ctx, skuId);
    if (hasStock) {
      throw new BadRequestException(
        'Cannot retire SKU with on-hand stock.',
      );
    }
    const coll = await this.collections.skus(ctx.tenantUri);
    const now = new Date();
    await coll.updateOne(
      { skuId },
      { $set: { status: 'retired', updatedAt: now } },
    );
    const idColl = await this.collections.skuIdentifiers(ctx.tenantUri);
    await idColl.updateMany(
      { skuId, status: 'active' },
      { $set: { status: 'superseded', updatedAt: now } },
    );
    await this.audit.append(ctx, 'sku.retired', skuId, {
      reason: dto.reason?.trim() ?? '',
    });
    return { skuId, status: 'retired' };
  }

  private async listIdentifiers(ctx: WmsRequestContext, skuId: string) {
    const coll = await this.collections.skuIdentifiers(ctx.tenantUri);
    return coll.find({ skuId }).sort({ namespace: 1, createdAt: 1 }).toArray();
  }

  private async findSku(
    ctx: WmsRequestContext,
    skuId: string,
  ): Promise<Record<string, unknown>> {
    await this.collections.ensurePhase02Indexes(ctx.tenantUri);
    const coll = await this.collections.skus(ctx.tenantUri);
    const doc = await coll.findOne({ skuId });
    if (!doc) {
      throw new NotFoundException('SKU not found.');
    }
    return doc as Record<string, unknown>;
  }

  private async checkScopeConflict(
    ctx: WmsRequestContext,
    scopeKey: string,
    excludeSkuId?: string,
  ): Promise<{ skuId: string; namespace: string } | null> {
    const coll = await this.collections.skuIdentifiers(ctx.tenantUri);
    const doc = await coll.findOne({ scopeKey, status: 'active' });
    if (!doc) {
      return null;
    }
    const sid = String(doc.skuId);
    if (excludeSkuId && sid === excludeSkuId) {
      return null;
    }
    return { skuId: sid, namespace: String(doc.namespace) };
  }

  /** Same namespace + value but different scope metadata — ambiguity hint only. */
  private async findAmbiguousMatches(
    ctx: WmsRequestContext,
    namespace: WmsSkuNamespace,
    value: string,
    metadata: Record<string, unknown>,
  ) {
    const coll = await this.collections.skuIdentifiers(ctx.tenantUri);
    const v = value.trim();
    const docs = await coll
      .find({ namespace, value: v, status: 'active' })
      .limit(20)
      .toArray();
    const scopeKey = identifierScopeKey(namespace, value, metadata);
    return docs
      .filter((d: Record<string, unknown>) => String(d.scopeKey) !== scopeKey)
      .map((d: Record<string, unknown>) => ({
        skuId: String(d.skuId),
        scopeKey: String(d.scopeKey),
        metadata: d.metadata,
      }));
  }

  private mapSku(doc: Record<string, unknown>) {
    return {
      skuId: String(doc.skuId),
      displayName: String(doc.displayName ?? ''),
      description: String(doc.description ?? ''),
      primaryUom: String(doc.primaryUom ?? ''),
      variantGroupId: doc.variantGroupId ?? null,
      variantRevision: doc.variantRevision ?? null,
      dimensions: doc.dimensions ?? {},
      packaging: doc.packaging ?? {},
      status: String(doc.status ?? 'active'),
      mergedIntoSkuId: doc.mergedIntoSkuId ?? null,
      createdAt: doc.createdAt,
      updatedAt: doc.updatedAt,
    };
  }

  private sanitizeConflictBody(
    ctx: WmsRequestContext,
    conflict: { skuId: string; namespace: string },
    scopeKey: string,
  ) {
    if (ctx.isWmsAdmin) {
      return {
        message:
          'An active identifier already exists for this namespace and scope.',
        existingSkuId: conflict.skuId,
        namespace: conflict.namespace,
        scopeKey,
      };
    }
    return {
      message:
        'An active identifier already exists for this namespace and scope.',
      conflict: true,
      scopeKey,
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