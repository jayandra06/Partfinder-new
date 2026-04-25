import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { CreateColumnDto } from './dto/create-column.dto';
import { CreateRowDto, UpdateRowDto } from './dto/create-row.dto';
import { CreateTemplateDto } from './dto/create-template.dto';
import { UpdateColumnDto } from './dto/update-column.dto';
import { TemplateColumn, TemplateColumnDocument } from './schemas/template-column.schema';
import { TemplateCell, TemplateCellDocument } from './schemas/template-cell.schema';
import { TemplateRow, TemplateRowDocument } from './schemas/template-row.schema';
import { Template, TemplateDocument } from './schemas/template.schema';
import { RedisService } from '../common/redis/redis.service';

@Injectable()
export class TemplatesService {
  constructor(
    @InjectModel(Template.name) private readonly templates: Model<TemplateDocument>,
    @InjectModel(TemplateColumn.name) private readonly columns: Model<TemplateColumnDocument>,
    @InjectModel(TemplateRow.name) private readonly rows: Model<TemplateRowDocument>,
    @InjectModel(TemplateCell.name) private readonly cells: Model<TemplateCellDocument>,
    private readonly redis: RedisService,
  ) {}

  async create(orgId: string, dto: CreateTemplateDto) {
    const doc = await this.templates.create({ orgId, name: dto.name.trim() });
    return doc.toObject();
  }

  async list(orgId: string) {
    const cacheKey = this.cacheKey(orgId, 'list');
    const cached = await this.redis.getJson<Array<Record<string, unknown>>>(cacheKey);
    if (cached) {
      return cached;
    }

    const data = await this.templates.find({ orgId }).sort({ name: 1 }).lean();
    await this.redis.setJson(cacheKey, data, 60);
    return data;
  }

  async getOne(orgId: string, id: string): Promise<Record<string, unknown>> {
    this.ensureObjectId(id, 'template id');
    const cacheKey = this.cacheKey(orgId, 'one', id);
    const cached = await this.redis.getJson<Record<string, unknown>>(cacheKey);
    if (cached) {
      return cached;
    }

    const template = await this.templates.findOne({ _id: id, orgId }).lean();
    if (!template) throw new NotFoundException('Template not found');
    const columns = await this.columns.find({ templateId: id }).sort({ order: 1 }).lean();
    const data = { ...template, columns };
    await this.redis.setJson(cacheKey, data, 60);
    return data;
  }

  async rename(orgId: string, id: string, dto: CreateTemplateDto) {
    this.ensureObjectId(id, 'template id');
    const updated = await this.templates.findOneAndUpdate(
      { _id: id, orgId },
      { $set: { name: dto.name.trim() } },
      { new: true },
    ).lean();
    if (!updated) throw new NotFoundException('Template not found');
    await this.invalidateTemplateCache(orgId, id);
    return updated;
  }

  async delete(orgId: string, id: string) {
    this.ensureObjectId(id, 'template id');
    const deleted = await this.templates.findOneAndDelete({ _id: id, orgId }).lean();
    if (!deleted) throw new NotFoundException('Template not found');

    const rowDocs = await this.rows.find({ templateId: id, orgId }, { _id: 1 }).lean();
    const rowIds = rowDocs.map((r) => r._id.toString());

    await Promise.all([
      this.columns.deleteMany({ templateId: id }),
      this.rows.deleteMany({ templateId: id, orgId }),
      rowIds.length ? this.cells.deleteMany({ rowId: { $in: rowIds } }) : Promise.resolve(),
    ]);

    await this.invalidateTemplateCache(orgId, id);
    return { deleted: true };
  }

  async addColumn(orgId: string, templateId: string, dto: CreateColumnDto) {
    await this.ensureTemplate(orgId, templateId);
    const type = dto.type ?? 'text';
    const doc = await this.columns.create({
      templateId,
      name: dto.name.trim(),
      order: dto.order,
      type,
      isLink: type === 'link',
    });
    await this.invalidateTemplateCache(orgId, templateId);
    return doc.toObject();
  }

  async updateColumn(orgId: string, templateId: string, colId: string, dto: UpdateColumnDto) {
    await this.ensureTemplate(orgId, templateId);
    this.ensureObjectId(colId, 'column id');

    const set: Record<string, unknown> = {};
    if (dto.name !== undefined) set.name = dto.name.trim();
    if (dto.type !== undefined) {
      set.type = dto.type;
      set.isLink = dto.type === 'link';
    }

    const updated = await this.columns.findOneAndUpdate(
      { _id: colId, templateId },
      { $set: set },
      { new: true },
    ).lean();

    if (!updated) throw new NotFoundException('Column not found');
    await this.invalidateTemplateCache(orgId, templateId);
    return updated;
  }

  async deleteColumn(orgId: string, templateId: string, colId: string) {
    await this.ensureTemplate(orgId, templateId);
    this.ensureObjectId(colId, 'column id');

    const removed = await this.columns.findOneAndDelete({ _id: colId, templateId }).lean();
    if (!removed) throw new NotFoundException('Column not found');

    const rows = await this.rows.find({ templateId, orgId }, { _id: 1 }).lean();
    const rowIds = rows.map((r) => r._id.toString());
    if (rowIds.length) {
      await this.cells.deleteMany({ rowId: { $in: rowIds }, columnId: colId });
    }

    await this.invalidateTemplateCache(orgId, templateId);
    return { deleted: true };
  }

  async addRow(orgId: string, templateId: string, dto: CreateRowDto): Promise<Record<string, unknown>> {
    await this.ensureTemplate(orgId, templateId);
    const row = await this.rows.create({ templateId, orgId });

    const validColumns = await this.columns.find({ templateId }, { _id: 1 }).lean();
    const validIds = new Set(validColumns.map((c) => c._id.toString()));

    const cellsToInsert = dto.cells
      .filter((c) => validIds.has(c.columnId))
      .map((c) => ({ rowId: row._id.toString(), columnId: c.columnId, value: c.value ?? '' }));

    if (cellsToInsert.length) {
      await this.cells.insertMany(cellsToInsert, { ordered: false });
    }

    await this.invalidateTemplateCache(orgId, templateId);
    return this.getRowById(row._id.toString());
  }

  async getRows(
    orgId: string,
    templateId: string,
    page = 1,
    limit = 100,
  ): Promise<Record<string, unknown>> {
    await this.ensureTemplate(orgId, templateId);
    const safePage = Math.max(1, page);
    const safeLimit = Math.min(500, Math.max(1, limit));
    const skip = (safePage - 1) * safeLimit;

    const [rows, total] = await Promise.all([
      this.rows
        .find({ templateId, orgId })
        .sort({ createdAt: -1 })
        .skip(skip)
        .limit(safeLimit)
        .lean(),
      this.rows.countDocuments({ templateId, orgId }),
    ]);

    const rowIds = rows.map((r) => r._id.toString());
    const cells = rowIds.length
      ? await this.cells.find({ rowId: { $in: rowIds } }).lean()
      : [];

    const cellMap = new Map<string, { columnId: string; value: string }[]>();
    for (const cell of cells) {
      const arr = cellMap.get(cell.rowId) ?? [];
      arr.push({ columnId: cell.columnId, value: cell.value });
      cellMap.set(cell.rowId, arr);
    }

    return {
      page: safePage,
      limit: safeLimit,
      total,
      rows: rows.map((r) => ({ ...r, cells: cellMap.get(r._id.toString()) ?? [] })),
    };
  }

  async updateRow(
    orgId: string,
    templateId: string,
    rowId: string,
    dto: UpdateRowDto,
  ): Promise<Record<string, unknown>> {
    await this.ensureTemplate(orgId, templateId);
    this.ensureObjectId(rowId, 'row id');
    const row = await this.rows.findOne({ _id: rowId, templateId, orgId }).lean();
    if (!row) throw new NotFoundException('Row not found');

    if (dto.cells?.length) {
      for (const cell of dto.cells) {
        await this.cells.findOneAndUpdate(
          { rowId, columnId: cell.columnId },
          { $set: { value: cell.value ?? '' } },
          { upsert: true, new: true },
        );
      }
    }

    await this.invalidateTemplateCache(orgId, templateId);
    return this.getRowById(rowId);
  }

  async deleteRow(orgId: string, templateId: string, rowId: string) {
    await this.ensureTemplate(orgId, templateId);
    this.ensureObjectId(rowId, 'row id');

    const removed = await this.rows.findOneAndDelete({ _id: rowId, templateId, orgId }).lean();
    if (!removed) throw new NotFoundException('Row not found');
    await this.cells.deleteMany({ rowId });
    await this.invalidateTemplateCache(orgId, templateId);
    return { deleted: true };
  }

  private async ensureTemplate(orgId: string, templateId: string) {
    this.ensureObjectId(templateId, 'template id');
    const template = await this.templates.findOne({ _id: templateId, orgId }).lean();
    if (!template) throw new NotFoundException('Template not found');
    return template;
  }

  private async getRowById(rowId: string): Promise<Record<string, unknown>> {
    const row = await this.rows.findById(rowId).lean();
    const cells = await this.cells.find({ rowId }).lean();
    return {
      ...row,
      cells: cells.map((c) => ({ columnId: c.columnId, value: c.value })),
    };
  }

  private ensureObjectId(value: string, label: string) {
    if (!Types.ObjectId.isValid(value)) {
      throw new BadRequestException(`Invalid ${label}`);
    }
  }

  private cacheKey(orgId: string, scope: string, ...parts: string[]) {
    return ['pf', 'templates', orgId, scope, ...parts].join(':');
  }

  private async invalidateTemplateCache(orgId: string, templateId?: string) {
    await this.redis.delete(this.cacheKey(orgId, 'list'));
    if (templateId) {
      await this.redis.delete(this.cacheKey(orgId, 'one', templateId));
    }
  }
}
