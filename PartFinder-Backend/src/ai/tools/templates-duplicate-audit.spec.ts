import 'reflect-metadata';
import { Types } from 'mongoose';
import { RedisService } from '../../common/redis/redis.service';
import { TemplatesService } from '../../templates/templates.service';

describe('TemplatesService.findDuplicateRecords (Batch 2 Audit Fixes)', () => {
  const orgId = '884470';
  const templateId = new Types.ObjectId().toString();
  const partColId = new Types.ObjectId().toString();
  const vendorColId = new Types.ObjectId().toString();

  function createServiceWithCapturedPipeline() {
    const templatesModel = {
      findOne: jest.fn().mockReturnValue({
        lean: jest.fn().mockResolvedValue({
          _id: templateId,
          orgId,
          name: 'Master Parts',
        }),
      }),
    };
    const columnsModel = {
      find: jest.fn().mockReturnValue({
        sort: jest.fn().mockReturnValue({
          lean: jest.fn().mockResolvedValue([
            { _id: partColId, templateId, name: 'PartNumber', order: 0 },
            { _id: vendorColId, templateId, name: 'Vendor', order: 1 },
          ]),
        }),
      }),
    };
    const rowsModel = {
      aggregate: jest.fn().mockResolvedValue([
        {
          totals: [{ totalDuplicateGroups: 0 }],
          groups: [],
        },
      ]),
    };
    const cellsModel = {};
    const redisService = {} as RedisService;

    const service = new TemplatesService(
      templatesModel as never,
      columnsModel as never,
      rowsModel as never,
      cellsModel as never,
      redisService,
    );

    return { service, rowsModel };
  }

  /**
   * Evaluates the composite-key grouping and ignoreEmpty behavior of the
   * generated MongoDB aggregation pipeline against in-memory rows.
   */
  function evaluateDuplicatePipeline(
    pipeline: Array<Record<string, unknown>>,
    dataset: Array<{ rowId: string; cells: Record<string, string | null | undefined> }>,
    keyCols: Array<{ colId: string; name: string }>,
    caseSensitive: boolean,
  ) {
    const hasIgnoreEmptyMatch = pipeline.some(
      (stage) =>
        '$match' in stage &&
        typeof stage.$match === 'object' &&
        stage.$match !== null &&
        'allKeyColumnsNonEmpty' in (stage.$match as Record<string, unknown>),
    );

    const addFieldsStage = pipeline.find(
      (stage) =>
        '$addFields' in stage &&
        typeof stage.$addFields === 'object' &&
        stage.$addFields !== null &&
        'allKeyColumnsNonEmpty' in (stage.$addFields as Record<string, unknown>),
    ) as { $addFields: { allKeyColumnsNonEmpty: Record<string, unknown> } };

    if (keyCols.length > 1) {
      expect(addFieldsStage.$addFields.allKeyColumnsNonEmpty).toHaveProperty(
        '$and',
      );
    }

    const groups = new Map<
      string,
      {
        sampleKeyValues: Record<string, string>;
        rowIds: string[];
      }
    >();

    for (const row of dataset) {
      const rawKeyObj: Record<string, string> = {};
      const keyObj: Record<string, string> = {};
      let allNonEmpty = true;

      for (const col of keyCols) {
        const trimmed = String(row.cells[col.colId] ?? '').trim();
        const normalized = caseSensitive ? trimmed : trimmed.toLowerCase();
        rawKeyObj[col.name] = trimmed;
        keyObj[col.name] = normalized;
        if (normalized === '') {
          allNonEmpty = false;
        }
      }

      if (hasIgnoreEmptyMatch && !allNonEmpty) {
        continue;
      }

      const groupKey = JSON.stringify(keyObj);
      const existing = groups.get(groupKey);
      if (!existing) {
        groups.set(groupKey, {
          sampleKeyValues: rawKeyObj,
          rowIds: [row.rowId],
        });
      } else {
        existing.rowIds.push(row.rowId);
      }
    }

    return Array.from(groups.values())
      .filter((g) => g.rowIds.length > 1)
      .map((g) => ({
        keyValues: g.sampleKeyValues,
        duplicateCount: g.rowIds.length,
        rowIds: g.rowIds.slice(0, 20),
      }));
  }

  it('uses $and for ignoreEmpty: true and excludes partially/fully empty composite keys', async () => {
    const { service, rowsModel } = createServiceWithCapturedPipeline();

    await service.findDuplicateRecords(orgId, templateId, {
      columnNames: ['PartNumber', 'Vendor'],
      ignoreEmpty: true,
    });

    const pipeline = rowsModel.aggregate.mock.calls[0][0] as Array<
      Record<string, unknown>
    >;

    const dataset = [
      // 1. All key columns populated (duplicate pair)
      { rowId: 'r1', cells: { [partColId]: 'P1001', [vendorColId]: 'Acme' } },
      { rowId: 'r2', cells: { [partColId]: 'P1001', [vendorColId]: 'Acme' } },
      // 2. One key column empty (duplicate pair that must be excluded)
      { rowId: 'r3', cells: { [partColId]: 'P2002', [vendorColId]: '' } },
      { rowId: 'r4', cells: { [partColId]: 'P2002', [vendorColId]: '' } },
      // 3. One key column whitespace-only (duplicate pair that must be excluded)
      { rowId: 'r5', cells: { [partColId]: 'P3003', [vendorColId]: '   ' } },
      { rowId: 'r6', cells: { [partColId]: 'P3003', [vendorColId]: ' ' } },
      // 4. All key columns empty (duplicate pair that must be excluded)
      { rowId: 'r7', cells: { [partColId]: '', [vendorColId]: null } },
      { rowId: 'r8', cells: { [partColId]: '  ', [vendorColId]: undefined } },
    ];

    const groups = evaluateDuplicatePipeline(
      pipeline,
      dataset,
      [
        { colId: partColId, name: 'PartNumber' },
        { colId: vendorColId, name: 'Vendor' },
      ],
      false,
    );

    expect(groups).toEqual([
      {
        keyValues: { PartNumber: 'P1001', Vendor: 'Acme' },
        duplicateCount: 2,
        rowIds: ['r1', 'r2'],
      },
    ]);
  });

  it('includes empty/partial composite keys when ignoreEmpty is false', async () => {
    const { service, rowsModel } = createServiceWithCapturedPipeline();

    await service.findDuplicateRecords(orgId, templateId, {
      columnNames: ['PartNumber', 'Vendor'],
      ignoreEmpty: false,
    });

    const pipeline = rowsModel.aggregate.mock.calls[0][0] as Array<
      Record<string, unknown>
    >;

    const dataset = [
      { rowId: 'r1', cells: { [partColId]: 'P2002', [vendorColId]: '' } },
      { rowId: 'r2', cells: { [partColId]: 'P2002', [vendorColId]: '  ' } },
      { rowId: 'r3', cells: { [partColId]: '', [vendorColId]: '' } },
      { rowId: 'r4', cells: { [partColId]: '', [vendorColId]: null } },
    ];

    const groups = evaluateDuplicatePipeline(
      pipeline,
      dataset,
      [
        { colId: partColId, name: 'PartNumber' },
        { colId: vendorColId, name: 'Vendor' },
      ],
      false,
    );

    expect(groups).toHaveLength(2);
    expect(groups[0]).toEqual({
      keyValues: { PartNumber: 'P2002', Vendor: '' },
      duplicateCount: 2,
      rowIds: ['r1', 'r2'],
    });
    expect(groups[1]).toEqual({
      keyValues: { PartNumber: '', Vendor: '' },
      duplicateCount: 2,
      rowIds: ['r3', 'r4'],
    });
  });

  it('groups case-insensitively while preserving original casing in keyValues via $first', async () => {
    const { service, rowsModel } = createServiceWithCapturedPipeline();

    await service.findDuplicateRecords(orgId, templateId, {
      columnNames: ['PartNumber'],
      caseSensitive: false,
    });

    const pipeline = rowsModel.aggregate.mock.calls[0][0] as Array<
      Record<string, unknown>
    >;

    const groupStage = pipeline.find((stage) => '$group' in stage) as {
      $group: { sampleKeyValues: unknown };
    };
    expect(groupStage.$group.sampleKeyValues).toEqual({ $first: '$rawKeyObj' });

    const dataset = [
      { rowId: 'r1', cells: { [partColId]: 'P1001' } },
      { rowId: 'r2', cells: { [partColId]: 'p1001' } },
      { rowId: 'r3', cells: { [partColId]: 'P1001' } },
    ];

    const groups = evaluateDuplicatePipeline(
      pipeline,
      dataset,
      [{ colId: partColId, name: 'PartNumber' }],
      false,
    );

    expect(groups).toEqual([
      {
        keyValues: { PartNumber: 'P1001' },
        duplicateCount: 3,
        rowIds: ['r1', 'r2', 'r3'],
      },
    ]);
  });
});
