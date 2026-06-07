import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { CreateRelationDto, UpdateRelationDto } from './dto/create-relation.dto';
import { RelationDisplayColumn, RelationDisplayColumnDocument } from './schemas/relation-display-column.schema';
import { RelationMatchKey, RelationMatchKeyDocument } from './schemas/relation-match-key.schema';
import { WorksheetRelation, WorksheetRelationDocument } from './schemas/worksheet-relation.schema';
import { RedisService } from '../common/redis/redis.service';

@Injectable()
export class RelationsService {
  constructor(
    @InjectModel(WorksheetRelation.name) private readonly relations: Model<WorksheetRelationDocument>,
    @InjectModel(RelationMatchKey.name) private readonly matchKeys: Model<RelationMatchKeyDocument>,
    @InjectModel(RelationDisplayColumn.name) private readonly displayColumns: Model<RelationDisplayColumnDocument>,
    private readonly redis: RedisService,
  ) {}

  async create(orgId: string, dto: CreateRelationDto) {
    const relation = await this.relations.create({
      orgId,
      primaryTemplateId: dto.primaryTemplateId,
      lookupTemplateId: dto.lookupTemplateId,
      name: dto.name.trim(),
      triggerColumn: dto.triggerColumn,
      menuLabel: dto.menuLabel.trim(),
    });

    const relationId = relation._id.toString();
    if (dto.matchKeys.length) {
      await this.matchKeys.insertMany(
        dto.matchKeys.map((k) => ({ relationId, sourceColumn: k.sourceColumn, targetColumn: k.targetColumn })),
      );
    }
    if (dto.displayColumns.length) {
      await this.displayColumns.insertMany(
        dto.displayColumns.map((c) => ({ relationId, columnName: c })),
      );
    }

    await this.invalidateCache(orgId);
    return this.getOne(orgId, relationId);
  }

  async list(orgId: string) {
    const cacheKey = this.cacheKey(orgId, 'list');
    const cached = await this.redis.getJson<Array<Record<string, unknown>>>(cacheKey);
    if (cached) {
      return cached;
    }

    const relations = await this.relations.find({ orgId }).sort({ createdAt: -1 }).lean();
    const result = await Promise.all(relations.map((r) => this.composeRelation(r._id.toString(), r)));

    await this.redis.setJson(cacheKey, result, 60);
    return result;
  }

  async getOne(orgId: string, id: string) {
    this.ensureObjectId(id, 'relation id');

    const cacheKey = this.cacheKey(orgId, 'one', id);
    const cached = await this.redis.getJson<Record<string, unknown>>(cacheKey);
    if (cached) {
      return cached;
    }

    const relation = await this.relations.findOne({ _id: id, orgId }).lean();
    if (!relation) throw new NotFoundException('Relation not found');
    const result = await this.composeRelation(id, relation);

    await this.redis.setJson(cacheKey, result, 60);
    return result;
  }

  async update(orgId: string, id: string, dto: UpdateRelationDto) {
    this.ensureObjectId(id, 'relation id');
    const relation = await this.relations.findOne({ _id: id, orgId }).lean();
    if (!relation) throw new NotFoundException('Relation not found');

    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.triggerColumn !== undefined) set.triggerColumn = dto.triggerColumn;
    if (dto.menuLabel !== undefined) set.menuLabel = dto.menuLabel.trim();
    if (Object.keys(set).length) {
      await this.relations.updateOne({ _id: id, orgId }, { $set: set });
    }

    if (dto.matchKeys) {
      await this.matchKeys.deleteMany({ relationId: id });
      if (dto.matchKeys.length) {
        await this.matchKeys.insertMany(dto.matchKeys.map((k) => ({ relationId: id, sourceColumn: k.sourceColumn, targetColumn: k.targetColumn })));
      }
    }

    if (dto.displayColumns) {
      await this.displayColumns.deleteMany({ relationId: id });
      if (dto.displayColumns.length) {
        await this.displayColumns.insertMany(dto.displayColumns.map((c) => ({ relationId: id, columnName: c })));
      }
    }

    await this.invalidateCache(orgId, id);
    return this.getOne(orgId, id);
  }

  async remove(orgId: string, id: string) {
    this.ensureObjectId(id, 'relation id');
    const relation = await this.relations.findOneAndDelete({ _id: id, orgId }).lean();
    if (!relation) throw new NotFoundException('Relation not found');

    await Promise.all([
      this.matchKeys.deleteMany({ relationId: id }),
      this.displayColumns.deleteMany({ relationId: id }),
    ]);

    await this.invalidateCache(orgId, id);
    return { deleted: true };
  }

  private async composeRelation(id: string, relation: WorksheetRelationDocument | Record<string, any>) {
    const [matchKeys, displayColumns] = await Promise.all([
      this.matchKeys.find({ relationId: id }).lean(),
      this.displayColumns.find({ relationId: id }).lean(),
    ]);

    return {
      ...relation,
      matchKeys: matchKeys.map((m) => ({ sourceColumn: m.sourceColumn, targetColumn: m.targetColumn })),
      displayColumns: displayColumns.map((d) => d.columnName),
    };
  }

  private cacheKey(orgId: string, scope: string, ...parts: string[]): string {
    return ['pf', 'relations', orgId, scope, ...parts].join(':');
  }

  private async invalidateCache(orgId: string, relationId?: string): Promise<void> {
    await this.redis.delete(this.cacheKey(orgId, 'list'));
    if (relationId) {
      await this.redis.delete(this.cacheKey(orgId, 'one', relationId));
    }
    // Also invalidate view-data cache since relations affect it
    await this.redis.delete(['pf', 'view-data', orgId].join(':'));
  }

  private ensureObjectId(value: string, label: string) {
    if (!Types.ObjectId.isValid(value)) {
      throw new BadRequestException(`Invalid ${label}`);
    }
  }
}
