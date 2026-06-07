import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { Part, PartDocument } from '../parts/schemas/part.schema';
import { Template, TemplateDocument } from '../templates/schemas/template.schema';
import { RedisService } from '../common/redis/redis.service';

@Injectable()
export class DashboardService {
  constructor(
    @InjectModel(Part.name) private readonly parts: Model<PartDocument>,
    @InjectModel(Template.name) private readonly templates: Model<TemplateDocument>,
    private readonly redis: RedisService,
  ) {}

  async stats(orgId: string) {
    // Debug: log the orgId
    if (!orgId) {
      console.warn('[Dashboard.stats] Missing orgId - returning defaults');
    }
    
    const cacheKey = this.cacheKey(orgId, 'stats');
    const cached = await this.redis.getJson<Record<string, unknown>>(cacheKey);
    if (cached) {
      console.log(`[Dashboard.stats] Cache hit for ${cacheKey}`);
      return cached;
    }

    console.log(`[Dashboard.stats] Querying with orgId: ${orgId}`);
    const [allParts, activeTemplates] = await Promise.all([
      this.parts.find({ orgId }).lean(),
      this.templates.countDocuments({ orgId }),
    ]);
    console.log(`[Dashboard.stats] Found ${activeTemplates} templates for orgId: ${orgId}`);

    const lowStock = allParts.filter((p) => {
      const qty = Number(p.metadata?.quantity ?? p.metadata?.qty ?? '');
      return Number.isFinite(qty) && qty > 0 && qty < 10;
    }).length;

    const result = {
      totalParts: allParts.length,
      lowStock,
      activeTemplates,
      importSuccessRate: 99.4,
      recentActivity: [
        'Template updated',
        'Parts imported',
        'Low stock alert detected',
      ],
    };

    // Cache dashboard stats for 45 seconds
    await this.redis.setJson(cacheKey, result, 45);
    return result;
  }

  async trend(orgId: string) {
    const cacheKey = this.cacheKey(orgId, 'trend');
    const cached = await this.redis.getJson<Array<Record<string, unknown>>>(cacheKey);
    if (cached) {
      return cached;
    }

    const count = await this.parts.countDocuments({ orgId });
    const points = Array.from({ length: 12 }).map((_, i) => ({
      label: `M${i + 1}`,
      value: Math.max(0, Math.round(count * (0.7 + i * 0.03))),
    }));

    // Cache trend for 2 minutes
    await this.redis.setJson(cacheKey, points, 120);
    return points;
  }

  async debugGetTemplateCount(orgId: string): Promise<number> {
    console.log(`[Dashboard.debug] Checking template count for orgId: '${orgId}'`);
    const count = await this.templates.countDocuments({ orgId });
    console.log(`[Dashboard.debug] Found ${count} templates`);
    
    // Also log all templates to see what's in the database
    const allTemplates = await this.templates.find({}).lean();
    console.log(`[Dashboard.debug] Total templates in DB: ${allTemplates.length}`);
    console.log(`[Dashboard.debug] All template orgIds:`, allTemplates.map(t => ({ name: t.name, orgId: t.orgId })));
    
    return count;
  }

  private cacheKey(orgId: string, scope: string): string {
    return ['pf', 'dashboard', orgId, scope].join(':');
  }
}
