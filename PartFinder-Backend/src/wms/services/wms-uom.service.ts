import {
  BadRequestException,
  ConflictException,
  ForbiddenException,
  Injectable,
} from '@nestjs/common';
import type { WmsRequestContext } from '../wms.types';
import {
  WMS_STANDARD_UOM,
  isValidUomCode,
  type WmsUomCategory,
} from '../wms-uom.constants';
import { assertWmsAdminWrite } from '../wms-scope.util';
import { WmsTenantCollectionsService } from './wms-tenant-collections.service';

@Injectable()
export class WmsUomService {
  constructor(private readonly collections: WmsTenantCollectionsService) {}

  listStandard() {
    return WMS_STANDARD_UOM;
  }

  async listCustom(ctx: WmsRequestContext) {
    await this.collections.ensurePhase02Indexes(ctx.tenantUri);
    const coll = await this.collections.uomDefinitions(ctx.tenantUri);
    const docs = await coll.find({}).sort({ code: 1 }).toArray();
    return docs.map((d: Record<string, unknown>) => ({
      code: String(d.code ?? ''),
      name: String(d.name ?? ''),
      category: String(d.category ?? 'custom'),
      isCustom: true,
    }));
  }

  async listAll(ctx: WmsRequestContext) {
    const custom = await this.listCustom(ctx);
    return [
      ...WMS_STANDARD_UOM.map((u) => ({ ...u, isCustom: false })),
      ...custom,
    ];
  }

  async createCustom(
    ctx: WmsRequestContext,
    code: string,
    name: string,
    category: WmsUomCategory = 'custom',
  ) {
    assertWmsAdminWrite(ctx);
    const normalized = code.trim().toUpperCase();
    if (!isValidUomCode(normalized)) {
      throw new BadRequestException('Invalid UOM code format.');
    }
    if (WMS_STANDARD_UOM.some((u) => u.code === normalized)) {
      throw new BadRequestException(
        'This code is reserved for a standard unit.',
      );
    }

    await this.collections.ensurePhase02Indexes(ctx.tenantUri);
    const coll = await this.collections.uomDefinitions(ctx.tenantUri);
    const now = new Date();
    try {
      await coll.insertOne({
        code: normalized,
        name: name.trim(),
        category,
        createdAt: now,
        updatedAt: now,
      });
    } catch (e: unknown) {
      if (this.isDup(e)) {
        throw new ConflictException(`UOM code "${normalized}" already exists.`);
      }
      throw e;
    }

    return { code: normalized, name: name.trim(), category, isCustom: true };
  }

  normalizeTemplateLabel(label: string): string {
    return label.trim().toLowerCase().replace(/\s+/g, ' ');
  }

  /** Map Excel template column/cell label to a registered UOM code (dry-run safe for imports). */
  async resolveTemplateUom(
    ctx: WmsRequestContext,
    templateId: string,
    sourceLabel: string,
  ): Promise<{ uomCode: string | null; matched: boolean }> {
    const sourceLabelNorm = this.normalizeTemplateLabel(sourceLabel);
    const direct = sourceLabel.trim().toUpperCase();
    if (WMS_STANDARD_UOM.some((u) => u.code === direct)) {
      return { uomCode: direct, matched: true };
    }
    await this.collections.ensurePhase02Indexes(ctx.tenantUri);
    const customColl = await this.collections.uomDefinitions(ctx.tenantUri);
    const custom = await customColl.findOne({ code: direct });
    if (custom) {
      return { uomCode: direct, matched: true };
    }
    const mapColl = await this.collections.uomTemplateMappings(ctx.tenantUri);
    const mapped = await mapColl.findOne({
      templateId: templateId.trim(),
      sourceLabelNorm,
    });
    if (mapped) {
      const code = String(mapped.uomCode ?? '').toUpperCase();
      await this.assertUomExists(ctx, code);
      return { uomCode: code, matched: true };
    }
    return { uomCode: null, matched: false };
  }

  async upsertTemplateMapping(
    ctx: WmsRequestContext,
    templateId: string,
    sourceLabel: string,
    uomCode: string,
  ) {
    assertWmsAdminWrite(ctx);
    const code = uomCode.trim().toUpperCase();
    await this.assertUomExists(ctx, code);
    await this.collections.ensurePhase02Indexes(ctx.tenantUri);
    const coll = await this.collections.uomTemplateMappings(ctx.tenantUri);
    const sourceLabelNorm = this.normalizeTemplateLabel(sourceLabel);
    const now = new Date();
    await coll.updateOne(
      { templateId: templateId.trim(), sourceLabelNorm },
      {
        $set: {
          sourceLabel: sourceLabel.trim(),
          uomCode: code,
          updatedAt: now,
        },
        $setOnInsert: { templateId: templateId.trim(), createdAt: now },
      },
      { upsert: true },
    );
    return { templateId: templateId.trim(), sourceLabel: sourceLabel.trim(), uomCode: code };
  }

  async listTemplateMappings(ctx: WmsRequestContext, templateId?: string) {
    await this.collections.ensurePhase02Indexes(ctx.tenantUri);
    const coll = await this.collections.uomTemplateMappings(ctx.tenantUri);
    const filter = templateId?.trim()
      ? { templateId: templateId.trim() }
      : {};
    const docs = await coll.find(filter).sort({ templateId: 1, sourceLabel: 1 }).toArray();
    return docs.map((d: Record<string, unknown>) => ({
      templateId: String(d.templateId),
      sourceLabel: String(d.sourceLabel),
      uomCode: String(d.uomCode),
    }));
  }

  async assertUomExists(ctx: WmsRequestContext, code: string): Promise<void> {
    const c = code.trim().toUpperCase();
    if (WMS_STANDARD_UOM.some((u) => u.code === c)) {
      return;
    }
    const coll = await this.collections.uomDefinitions(ctx.tenantUri);
    const found = await coll.findOne({ code: c });
    if (!found) {
      throw new BadRequestException(
        `Unknown unit of measure "${c}". Define a custom UOM or use a standard code.`,
      );
    }
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