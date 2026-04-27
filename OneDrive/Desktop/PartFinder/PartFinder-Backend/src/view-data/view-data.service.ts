import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { RelationDisplayColumn, RelationDisplayColumnDocument } from '../relations/schemas/relation-display-column.schema';
import { RelationMatchKey, RelationMatchKeyDocument } from '../relations/schemas/relation-match-key.schema';
import { WorksheetRelation, WorksheetRelationDocument } from '../relations/schemas/worksheet-relation.schema';
import { TemplateColumn, TemplateColumnDocument } from '../templates/schemas/template-column.schema';
import { TemplateCell, TemplateCellDocument } from '../templates/schemas/template-cell.schema';
import { TemplateRow, TemplateRowDocument } from '../templates/schemas/template-row.schema';
import { Template, TemplateDocument } from '../templates/schemas/template.schema';
import { RedisService } from '../common/redis/redis.service';

type RowCellsByColumn = Record<string, string>;

type HydratedRow = {
  rowId: string;
  cells: RowCellsByColumn;
};

@Injectable()
export class ViewDataService {
  constructor(
    @InjectModel(Template.name) private readonly templates: Model<TemplateDocument>,
    @InjectModel(TemplateColumn.name) private readonly columns: Model<TemplateColumnDocument>,
    @InjectModel(TemplateRow.name) private readonly rows: Model<TemplateRowDocument>,
    @InjectModel(TemplateCell.name) private readonly cells: Model<TemplateCellDocument>,
    @InjectModel(WorksheetRelation.name) private readonly relations: Model<WorksheetRelationDocument>,
    @InjectModel(RelationMatchKey.name) private readonly matchKeys: Model<RelationMatchKeyDocument>,
    @InjectModel(RelationDisplayColumn.name) private readonly displayColumns: Model<RelationDisplayColumnDocument>,
    private readonly redis: RedisService,
  ) {}

  async getEnrichedRows(orgId: string, primaryTemplateId: string): Promise<Array<Record<string, unknown>>> {
    if (!orgId || !orgId.trim()) {
      throw new BadRequestException('Missing X-Org-Id header');
    }
    this.ensureObjectId(primaryTemplateId, 'primaryTemplateId');
    const cacheKey = ['pf', 'view-data', orgId, primaryTemplateId].join(':');
    const cached = await this.redis.getJson<Array<Record<string, unknown>>>(cacheKey);
    if (cached) {
      return cached;
    }

    const primaryTemplate = await this.templates.findOne({ _id: primaryTemplateId, orgId }).lean();
    if (!primaryTemplate) {
      throw new NotFoundException('Primary template not found');
    }

    const primaryRows = await this.loadRowsForTemplate(orgId, primaryTemplateId);
    const relationDocs = await this.relations.find({ orgId, primaryTemplateId }).lean();

    if (!relationDocs.length) {
      const data = primaryRows.map((row) => ({ rowId: row.rowId, cells: row.cells, linkedData: {} }));
      await this.redis.setJson(cacheKey, data, 30);
      return data;
    }

    const relationIds = relationDocs.map((r) => r._id.toString());
    const [allMatchKeys, allDisplayColumns] = await Promise.all([
      this.matchKeys.find({ relationId: { $in: relationIds } }).lean(),
      this.displayColumns.find({ relationId: { $in: relationIds } }).lean(),
    ]);

    const lookupTemplateIds = Array.from(new Set(relationDocs.map((r) => r.lookupTemplateId)));
    const lookupRowsByTemplate = new Map<string, HydratedRow[]>();
    for (const lookupTemplateId of lookupTemplateIds) {
      lookupRowsByTemplate.set(
        lookupTemplateId,
        await this.loadRowsForTemplate(orgId, lookupTemplateId),
      );
    }

    const matchKeysByRelation = new Map<string, Array<{ sourceColumn: string; targetColumn: string }>>();
    for (const key of allMatchKeys) {
      const list = matchKeysByRelation.get(key.relationId) ?? [];
      list.push({ sourceColumn: key.sourceColumn, targetColumn: key.targetColumn });
      matchKeysByRelation.set(key.relationId, list);
    }

    const displayByRelation = new Map<string, string[]>();
    for (const d of allDisplayColumns) {
      const list = displayByRelation.get(d.relationId) ?? [];
      list.push(d.columnName);
      displayByRelation.set(d.relationId, list);
    }

    const data = primaryRows.map((primaryRow) => {
      const linkedData: Record<string, unknown> = {};

      for (const relation of relationDocs) {
        const relationId = relation._id.toString();
        const keys = matchKeysByRelation.get(relationId) ?? [];
        const lookupRows = lookupRowsByTemplate.get(relation.lookupTemplateId) ?? [];

        const matchedRow = lookupRows.find((lookupRow) =>
          keys.every((key) =>
            this.equalsIgnoreCase(
              primaryRow.cells[key.sourceColumn] ?? '',
              lookupRow.cells[key.targetColumn] ?? '',
            ),
          ),
        );

        if (!matchedRow) {
          linkedData[relationId] = {
            matched: false,
            displayValues: {},
            menuLabel: relation.menuLabel,
          };
          continue;
        }

        const displayColumns = displayByRelation.get(relationId) ?? [];
        const displayValues: Record<string, string> = {};

        if (!displayColumns.length) {
          for (const [key, value] of Object.entries(matchedRow.cells)) {
            displayValues[key] = value;
          }
        } else {
          for (const column of displayColumns) {
            displayValues[column] = matchedRow.cells[column] ?? '';
          }
        }

        linkedData[relationId] = {
          matched: true,
          displayValues,
          menuLabel: relation.menuLabel,
        };
      }

      return {
        rowId: primaryRow.rowId,
        cells: primaryRow.cells,
        linkedData,
      };
    });
    await this.redis.setJson(cacheKey, data, 30);
    return data;
  }

  private async loadRowsForTemplate(orgId: string, templateId: string): Promise<HydratedRow[]> {
    this.ensureObjectId(templateId, 'templateId');

    const [template, columnDocs, rowDocs] = await Promise.all([
      this.templates.findOne({ _id: templateId, orgId }).lean(),
      this.columns.find({ templateId }).lean(),
      this.rows.find({ templateId, orgId }).lean(),
    ]);

    if (!template) {
      throw new NotFoundException('Template not found');
    }

    const rowIds = rowDocs.map((row) => row._id.toString());
    const cellDocs = rowIds.length
      ? await this.cells.find({ rowId: { $in: rowIds } }).lean()
      : [];

    const columnNameById = new Map<string, string>();
    for (const c of columnDocs) {
      columnNameById.set(c._id.toString(), c.name);
    }

    const byRow = new Map<string, RowCellsByColumn>();
    for (const row of rowDocs) {
      byRow.set(row._id.toString(), {});
    }

    for (const cell of cellDocs) {
      const columnName = columnNameById.get(cell.columnId);
      if (!columnName) {
        continue;
      }

      const row = byRow.get(cell.rowId) ?? {};
      row[columnName] = cell.value ?? '';
      byRow.set(cell.rowId, row);
    }

    return rowDocs.map((row) => ({ rowId: row._id.toString(), cells: byRow.get(row._id.toString()) ?? {} }));
  }

  private equalsIgnoreCase(left: string, right: string): boolean {
    return left.trim().toLowerCase() === right.trim().toLowerCase();
  }

  private ensureObjectId(value: string, label: string) {
    if (!Types.ObjectId.isValid(value)) {
      throw new BadRequestException(`Invalid ${label}`);
    }
  }
}
