import { BadRequestException, Injectable, NotFoundException } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { parse } from 'csv-parse/sync';
import { Model, Types } from 'mongoose';
import { TemplateColumn, TemplateColumnDocument } from '../templates/schemas/template-column.schema';
import { TemplateCell, TemplateCellDocument } from '../templates/schemas/template-cell.schema';
import { TemplateRow, TemplateRowDocument } from '../templates/schemas/template-row.schema';
import { Template, TemplateDocument } from '../templates/schemas/template.schema';
import { RedisService } from '../common/redis/redis.service';

export type ImportJobState = {
  jobId: string;
  status: 'queued' | 'processing' | 'completed' | 'failed';
  totalRows: number;
  processedRows: number;
  failedRows: number;
  errors: string[];
  createdAtUtc: string;
  updatedAtUtc: string;
};

@Injectable()
export class ImportService {
  constructor(
    @InjectModel(Template.name) private readonly templates: Model<TemplateDocument>,
    @InjectModel(TemplateColumn.name) private readonly columns: Model<TemplateColumnDocument>,
    @InjectModel(TemplateRow.name) private readonly rows: Model<TemplateRowDocument>,
    @InjectModel(TemplateCell.name) private readonly cells: Model<TemplateCellDocument>,
    private readonly redis: RedisService,
  ) {}

  async enqueueImport(
    orgId: string,
    templateId: string,
    csvBuffer: Buffer,
    headerMap: Record<string, string>,
  ): Promise<{ jobId: string }> {
    this.ensureObjectId(templateId, 'templateId');
    const template = await this.templates.findOne({ _id: templateId, orgId }).lean();
    if (!template) {
      throw new NotFoundException('Template not found');
    }

    const jobId = new Types.ObjectId().toString();
    const now = new Date().toISOString();
    const state: ImportJobState = {
      jobId,
      status: 'queued',
      totalRows: 0,
      processedRows: 0,
      failedRows: 0,
      errors: [],
      createdAtUtc: now,
      updatedAtUtc: now,
    };

    await this.setJobState(orgId, templateId, state);

    setImmediate(async () => {
      await this.runImport(orgId, templateId, csvBuffer, headerMap, jobId);
    });

    return { jobId };
  }

  async getStatus(orgId: string, templateId: string): Promise<ImportJobState> {
    const jobId = await this.redis.getString(this.latestJobKey(orgId, templateId));
    if (!jobId || !jobId.trim()) {
      return {
        jobId: '',
        status: 'completed',
        totalRows: 0,
        processedRows: 0,
        failedRows: 0,
        errors: [],
        createdAtUtc: new Date().toISOString(),
        updatedAtUtc: new Date().toISOString(),
      };
    }

    const state = await this.redis.getJson<ImportJobState>(this.jobStateKey(orgId, templateId, jobId));
    if (!state) {
      throw new NotFoundException('Import job not found');
    }

    return state;
  }

  private async runImport(
    orgId: string,
    templateId: string,
    csvBuffer: Buffer,
    headerMap: Record<string, string>,
    jobId: string,
  ) {
    const job = await this.redis.getJson<ImportJobState>(this.jobStateKey(orgId, templateId, jobId));
    if (!job) return;

    job.status = 'processing';
    job.updatedAtUtc = new Date().toISOString();
    await this.setJobState(orgId, templateId, job);

    try {
      const columns = await this.columns.find({ templateId }).lean();
      const columnIdByName = new Map<string, string>(
        columns.map((c) => [c.name.trim().toLowerCase(), c._id.toString()]),
      );

      const mappedEntries = Object.entries(headerMap)
        .filter(([, target]) => !!target)
        .map(([csvHeader, targetColumn]) => {
          const colId = columnIdByName.get(targetColumn.trim().toLowerCase());
          return { csvHeader, targetColumn, colId };
        })
        .filter((m) => !!m.colId) as Array<{ csvHeader: string; targetColumn: string; colId: string }>;

      if (!mappedEntries.length) {
        throw new BadRequestException('No valid CSV header mappings were provided.');
      }

      const rows = parse(csvBuffer, {
        columns: true,
        skip_empty_lines: true,
        bom: true,
      }) as Array<Record<string, string>>;

      job.totalRows = rows.length;

      for (const row of rows) {
        const rowDoc = await this.rows.create({ orgId, templateId });
        const rowId = rowDoc._id.toString();

        const cellDocs = mappedEntries.map((m) => ({
          rowId,
          columnId: m.colId,
          value: String(row[m.csvHeader] ?? '').trim(),
        }));

        if (cellDocs.length) {
          await this.cells.insertMany(cellDocs, { ordered: false });
        }

        job.processedRows += 1;
        job.updatedAtUtc = new Date().toISOString();
        if (job.processedRows % 25 === 0 || job.processedRows === job.totalRows) {
          await this.setJobState(orgId, templateId, job);
        }
      }

      job.status = 'completed';
      job.updatedAtUtc = new Date().toISOString();
      await this.setJobState(orgId, templateId, job);
    } catch (error) {
      job.status = 'failed';
      job.failedRows = Math.max(1, job.totalRows - job.processedRows);
      job.errors.push(error instanceof Error ? error.message : 'Import failed');
      job.updatedAtUtc = new Date().toISOString();
      await this.setJobState(orgId, templateId, job);
    }
  }

  private ensureObjectId(value: string, label: string) {
    if (!Types.ObjectId.isValid(value)) {
      throw new BadRequestException(`Invalid ${label}`);
    }
  }

  private jobStateKey(orgId: string, templateId: string, jobId: string): string {
    return ['pf', 'import', orgId, templateId, jobId].join(':');
  }

  private latestJobKey(orgId: string, templateId: string): string {
    return ['pf', 'import', orgId, templateId, 'latest'].join(':');
  }

  private async setJobState(orgId: string, templateId: string, state: ImportJobState): Promise<void> {
    await this.redis.setString(this.latestJobKey(orgId, templateId), state.jobId, 60 * 60 * 24);
    await this.redis.setJson(this.jobStateKey(orgId, templateId, state.jobId), state, 60 * 60 * 24);
  }
}
