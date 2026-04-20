import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { CreatePartDto, UpdatePartDto } from './dto/part.dto';
import { Part, PartDocument } from './schemas/part.schema';

@Injectable()
export class PartsService {
  constructor(
    @InjectModel(Part.name) private readonly parts: Model<PartDocument>,
  ) {}

  async list(orgId: string, templateId?: string, search?: string, page = 1, limit = 100) {
    const safePage = Math.max(1, page);
    const safeLimit = Math.min(500, Math.max(1, limit));
    const skip = (safePage - 1) * safeLimit;

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

    return { page: safePage, limit: safeLimit, total, items };
  }

  async create(orgId: string, dto: CreatePartDto) {
    this.ensureObjectId(dto.templateId, 'templateId');
    const created = await this.parts.create({
      orgId,
      templateId: dto.templateId,
      rowId: dto.rowId,
      metadata: dto.metadata ?? {},
    });
    return created.toObject();
  }

  async getById(orgId: string, id: string) {
    this.ensureObjectId(id, 'part id');
    const part = await this.parts.findOne({ _id: id, orgId }).lean();
    if (!part) throw new NotFoundException('Part not found');
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
    return updated;
  }

  async remove(orgId: string, id: string) {
    this.ensureObjectId(id, 'part id');
    const deleted = await this.parts.findOneAndDelete({ _id: id, orgId }).lean();
    if (!deleted) throw new NotFoundException('Part not found');
    return { deleted: true };
  }

  async lowStock(orgId: string) {
    const parts = await this.parts.find({ orgId }).lean();
    const low = parts.filter((p) => {
      const rawQty = p.metadata?.quantity ?? p.metadata?.qty ?? '';
      const qty = Number(rawQty);
      return Number.isFinite(qty) && qty > 0 && qty < 10;
    });
    return { count: low.length, items: low };
  }

  private ensureObjectId(value: string, label: string) {
    if (!Types.ObjectId.isValid(value)) {
      throw new BadRequestException(`Invalid ${label}`);
    }
  }
}
