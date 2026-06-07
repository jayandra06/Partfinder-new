import { BadRequestException, Injectable, Logger, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { CreatePartDto, UpdatePartDto } from './dto/part.dto';
import { Part, PartDocument } from './schemas/part.schema';
import { RedisService } from '../common/redis/redis.service';
import { ElasticsearchService, EsPartDocument } from '../common/elasticsearch/elasticsearch.service';

@Injectable()
export class PartsService {
  private readonly logger = new Logger(PartsService.name);

  constructor(
    @InjectModel(Part.name) private readonly parts: Model<PartDocument>,
    private readonly redis: RedisService,
    private readonly es: ElasticsearchService,
  ) {}

  async list(orgId: string, templateId?: string, search?: string, page = 1, limit = 100) {
    const safePage = Math.max(1, page);
    const safeLimit = Math.min(500, Math.max(1, limit));
    const skip = (safePage - 1) * safeLimit;

    // If there's a search query and ES is available, use Elasticsearch
    if (search?.trim() && this.es.isAvailable()) {
      return this.searchViaElasticsearch(orgId, search.trim(), templateId, safePage, safeLimit);
    }

    // Cache key for non-search list queries
    const cacheKey = this.cacheKey(orgId, 'list', templateId ?? 'all', String(safePage), String(safeLimit));
    if (!search?.trim()) {
      const cached = await this.redis.getJson<Record<string, unknown>>(cacheKey);
      if (cached) {
        return cached;
      }
    }

    const query: Record<string, unknown> = { orgId };
    if (templateId?.trim()) {
      query.templateId = templateId.trim();
    }
    if (search?.trim()) {
      query.$or = [
        { rowId: { $regex: search.trim(), $options: 'i' } },
      ];
    }

    const [items, total] = await Promise.all([
      this.parts.find(query).sort({ createdAt: -1 }).skip(skip).limit(safeLimit).lean(),
      this.parts.countDocuments(query),
    ]);

    const result = { page: safePage, limit: safeLimit, total, items };

    // Cache non-search results for 30 seconds
    if (!search?.trim()) {
      await this.redis.setJson(cacheKey, result, 30);
    }

    return result;
  }

  async create(orgId: string, dto: CreatePartDto) {
    this.ensureObjectId(dto.templateId, 'templateId');
    const created = await this.parts.create({
      orgId,
      templateId: dto.templateId,
      rowId: dto.rowId,
      metadata: dto.metadata ?? {},
    });

    const part = created.toObject();

    // Invalidate list cache
    await this.invalidateListCache(orgId);

    // Index in Elasticsearch
    await this.indexPartInEs(part);

    return part;
  }

  async getById(orgId: string, id: string) {
    this.ensureObjectId(id, 'part id');

    // Try cache first
    const cacheKey = this.cacheKey(orgId, 'one', id);
    const cached = await this.redis.getJson<Record<string, unknown>>(cacheKey);
    if (cached) {
      return cached;
    }

    const part = await this.parts.findOne({ _id: id, orgId }).lean();
    if (!part) throw new NotFoundException('Part not found');

    // Cache for 60 seconds
    await this.redis.setJson(cacheKey, part, 60);
    return part;
  }

  async update(orgId: string, id: string, dto: UpdatePartDto) {
    this.ensureObjectId(id, 'part id');
    const set: Record<string, unknown> = {};
    if (dto.metadata) {
      set.metadata = dto.metadata;
    }

    const updated = await this.parts.findOneAndUpdate(
      { _id: id, orgId },
      { $set: set },
      { new: true },
    ).lean();
    if (!updated) throw new NotFoundException('Part not found');

    // Invalidate caches
    await this.invalidatePartCache(orgId, id);

    // Re-index in Elasticsearch
    await this.indexPartInEs(updated);

    return updated;
  }

  async remove(orgId: string, id: string) {
    this.ensureObjectId(id, 'part id');
    const deleted = await this.parts.findOneAndDelete({ _id: id, orgId }).lean();
    if (!deleted) throw new NotFoundException('Part not found');

    // Invalidate caches
    await this.invalidatePartCache(orgId, id);

    // Remove from Elasticsearch
    await this.es.removePart(id);

    return { deleted: true };
  }

  async lowStock(orgId: string) {
    // Cache low stock result for 60 seconds
    const cacheKey = this.cacheKey(orgId, 'lowstock');
    const cached = await this.redis.getJson<Record<string, unknown>>(cacheKey);
    if (cached) {
      return cached;
    }

    const parts = await this.parts.find({ orgId }).lean();
    const low = parts.filter((p) => {
      const rawQty = p.metadata?.quantity ?? p.metadata?.qty ?? '';
      const qty = Number(rawQty);
      return Number.isFinite(qty) && qty > 0 && qty < 10;
    });

    const result = { count: low.length, items: low };
    await this.redis.setJson(cacheKey, result, 60);
    return result;
  }

  /**
   * Sync all parts for an org into Elasticsearch (for initial setup or re-sync).
   */
  async syncToElasticsearch(orgId: string): Promise<{ synced: number }> {
    const allParts = await this.parts.find({ orgId }).lean();
    const docs: EsPartDocument[] = allParts.map((p) => this.buildEsDoc(p));
    await this.es.reindexOrg(orgId, docs);
    return { synced: docs.length };
  }

  // ─── Private helpers ───────────────────────────────────────────────

  private async searchViaElasticsearch(
    orgId: string,
    search: string,
    templateId: string | undefined,
    page: number,
    limit: number,
  ) {
    const esResult = await this.es.searchParts(orgId, search, templateId, page, limit);

    if (!esResult.ids.length) {
      return { page, limit, total: esResult.total, items: [] };
    }

    // Fetch actual docs from MongoDB by IDs returned from ES
    const items = await this.parts
      .find({ _id: { $in: esResult.ids }, orgId })
      .lean();

    // Preserve ES ordering
    const idOrder = new Map(esResult.ids.map((id, idx) => [id, idx]));
    items.sort((a, b) => (idOrder.get(a._id.toString()) ?? 0) - (idOrder.get(b._id.toString()) ?? 0));

    return { page, limit, total: esResult.total, items };
  }

  private buildEsDoc(part: any): EsPartDocument {
    const metadata = part.metadata ?? {};
    const allTextParts = [part.rowId ?? '', ...Object.values(metadata)].filter(Boolean);
    return {
      partId: part._id.toString(),
      orgId: part.orgId,
      templateId: part.templateId,
      rowId: part.rowId ?? '',
      metadata,
      allText: allTextParts.join(' '),
      createdAt: part.createdAt?.toISOString?.() ?? new Date().toISOString(),
    };
  }

  private async indexPartInEs(part: any): Promise<void> {
    const doc = this.buildEsDoc(part);
    await this.es.indexPart(doc);
  }

  private cacheKey(...parts: string[]): string {
    return ['pf', 'parts', ...parts].join(':');
  }

  private async invalidateListCache(orgId: string): Promise<void> {
    // Invalidate known list patterns - simple approach: delete the most common ones
    // For a production app you'd use Redis SCAN or key pattern deletion
    await this.redis.delete(this.cacheKey(orgId, 'lowstock'));
  }

  private async invalidatePartCache(orgId: string, partId: string): Promise<void> {
    await this.redis.delete(this.cacheKey(orgId, 'one', partId));
    await this.invalidateListCache(orgId);
  }

  private ensureObjectId(value: string, label: string) {
    if (!Types.ObjectId.isValid(value)) {
      throw new BadRequestException(`Invalid ${label}`);
    }
  }
}
