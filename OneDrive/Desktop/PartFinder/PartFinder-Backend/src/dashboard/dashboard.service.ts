import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { Part, PartDocument } from '../parts/schemas/part.schema';
import { Template, TemplateDocument } from '../templates/schemas/template.schema';

@Injectable()
export class DashboardService {
  constructor(
    @InjectModel(Part.name) private readonly parts: Model<PartDocument>,
    @InjectModel(Template.name) private readonly templates: Model<TemplateDocument>,
  ) {}

  async stats(orgId: string) {
    const [allParts, activeTemplates] = await Promise.all([
      this.parts.find({ orgId }).lean(),
      this.templates.countDocuments({ orgId }),
    ]);

    const lowStock = allParts.filter((p) => {
      const qty = Number(p.metadata?.quantity ?? p.metadata?.qty ?? '');
      return Number.isFinite(qty) && qty > 0 && qty < 10;
    }).length;

    return {
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
  }

  async trend(orgId: string) {
    const count = await this.parts.countDocuments({ orgId });
    const points = Array.from({ length: 12 }).map((_, i) => ({
      label: `M${i + 1}`,
      value: Math.max(0, Math.round(count * (0.7 + i * 0.03))),
    }));
    return points;
  }
}
