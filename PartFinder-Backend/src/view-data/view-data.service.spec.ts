import { BadRequestException, Logger, NotFoundException } from '@nestjs/common';
import { Types } from 'mongoose';
import { RedisService } from '../common/redis/redis.service';
import { ViewDataService } from './view-data.service';

type Doc = Record<string, any>;

/** Minimal in-memory mongoose model supporting equality and $in filters. */
function fakeModel(docs: Doc[]) {
  const matches = (doc: Doc, filter: Doc) =>
    Object.entries(filter).every(([k, v]) =>
      v && typeof v === 'object' && '$in' in v ? v.$in.includes(String(doc[k])) : String(doc[k]) === String(v),
    );
  return {
    find: jest.fn((filter: Doc) => ({ lean: async () => docs.filter((d) => matches(d, filter)) })),
    findOne: jest.fn((filter: Doc) => ({ lean: async () => docs.find((d) => matches(d, filter)) ?? null })),
  };
}

const oid = () => new Types.ObjectId();

function build() {
  const parts = oid();
  const vendors = oid();
  const [pPartNo, pVendor, vName, vPhone, vCity] = [oid(), oid(), oid(), oid(), oid()];
  const [r1, r2, v1, v2] = [oid(), oid(), oid(), oid()];
  const rel = oid();

  const templates = fakeModel([
    { _id: parts, orgId: 'org1', name: 'Parts' },
    { _id: vendors, orgId: 'org1', name: 'Vendors' },
  ]);
  const columns = fakeModel([
    { _id: pPartNo, templateId: parts.toString(), name: 'Part No' },
    { _id: pVendor, templateId: parts.toString(), name: 'Vendor' },
    { _id: vName, templateId: vendors.toString(), name: 'Name' },
    { _id: vPhone, templateId: vendors.toString(), name: 'Phone' },
    { _id: vCity, templateId: vendors.toString(), name: 'City' },
  ]);
  const rows = fakeModel([
    { _id: r1, templateId: parts.toString(), orgId: 'org1' },
    { _id: r2, templateId: parts.toString(), orgId: 'org1' },
    { _id: v1, templateId: vendors.toString(), orgId: 'org1' },
    { _id: v2, templateId: vendors.toString(), orgId: 'org1' },
  ]);
  const cells = fakeModel([
    { rowId: r1.toString(), columnId: pPartNo.toString(), value: 'PN-1' },
    { rowId: r1.toString(), columnId: pVendor.toString(), value: ' bosch ' },
    { rowId: r2.toString(), columnId: pPartNo.toString(), value: 'PN-2' },
    { rowId: r2.toString(), columnId: pVendor.toString(), value: 'Unknown Co' },
    { rowId: v1.toString(), columnId: vName.toString(), value: 'Bosch' },
    { rowId: v1.toString(), columnId: vPhone.toString(), value: '555-1' },
    { rowId: v1.toString(), columnId: vCity.toString(), value: 'Pune' },
    { rowId: v2.toString(), columnId: vName.toString(), value: 'Valeo' },
    { rowId: 'orphan', columnId: oid().toString(), value: 'ignored' },
  ]);
  const relations = fakeModel([
    { _id: rel, orgId: 'org1', primaryTemplateId: parts.toString(), lookupTemplateId: vendors.toString(), menuLabel: 'Vendor info' },
  ]);
  const matchKeys = fakeModel([{ relationId: rel.toString(), sourceColumn: 'Vendor', targetColumn: 'Name' }]);
  const displayColumns = fakeModel([{ relationId: rel.toString(), columnName: 'Phone' }]);
  const redis = new RedisService({ get: () => undefined } as any);
  const service = new ViewDataService(
    templates as any, columns as any, rows as any, cells as any,
    relations as any, matchKeys as any, displayColumns as any, redis,
  );
  return { service, ids: { parts, vendors, r1, r2, v1, rel }, templates, displayColumns };
}

describe('ViewDataService', () => {
  beforeAll(() => jest.spyOn(Logger.prototype, 'warn').mockImplementation(() => undefined));
  afterAll(() => jest.restoreAllMocks());

  it('requires an org id and a valid template id', async () => {
    const { service, ids } = build();

    await expect(service.getEnrichedRows(' ', ids.parts.toString())).rejects.toBeInstanceOf(BadRequestException);
    await expect(service.getEnrichedRows('org1', 'bad')).rejects.toBeInstanceOf(BadRequestException);
  });

  it('404s when the primary template belongs to another org', async () => {
    const { service, ids } = build();

    await expect(service.getEnrichedRows('org2', ids.parts.toString())).rejects.toBeInstanceOf(NotFoundException);
  });

  it('enriches primary rows with matching lookup rows (trimmed, case-insensitive)', async () => {
    const { service, ids } = build();

    const rows: any[] = await service.getEnrichedRows('org1', ids.parts.toString());

    expect(rows).toHaveLength(2);
    const first = rows.find((r) => r.rowId === ids.r1.toString());
    expect(first.cells).toEqual({ 'Part No': 'PN-1', Vendor: ' bosch ' });
    expect(first.linkedData[ids.rel.toString()]).toEqual({
      matched: true,
      matchCount: 1,
      displayValues: { Phone: '555-1' },
      matches: [{ rowId: ids.v1.toString(), displayValues: { Phone: '555-1' } }],
      menuLabel: 'Vendor info',
    });
  });

  it('marks rows without a match as unmatched', async () => {
    const { service, ids } = build();

    const rows: any[] = await service.getEnrichedRows('org1', ids.parts.toString());
    const second = rows.find((r) => r.rowId === ids.r2.toString());

    expect(second.linkedData[ids.rel.toString()]).toEqual({
      matched: false,
      matchCount: 0,
      displayValues: {},
      matches: [],
      menuLabel: 'Vendor info',
    });
  });

  it('shows every lookup column when no display columns are configured', async () => {
    const { service, ids, displayColumns } = build();
    displayColumns.find.mockImplementation(() => ({ lean: async () => [] }) as any);

    const rows: any[] = await service.getEnrichedRows('org1', ids.parts.toString());
    const first = rows.find((r) => r.rowId === ids.r1.toString());

    expect(first.linkedData[ids.rel.toString()].displayValues).toEqual({ Name: 'Bosch', Phone: '555-1', City: 'Pune' });
  });

  it('returns rows with empty linkedData when the template has no relations', async () => {
    const { service, ids } = build();

    const rows: any[] = await service.getEnrichedRows('org1', ids.vendors.toString());

    expect(rows).toHaveLength(2);
    expect(rows.every((r) => Object.keys(r.linkedData).length === 0)).toBe(true);
  });

  it('caches results per org and template', async () => {
    const { service, ids, templates } = build();

    await service.getEnrichedRows('org1', ids.parts.toString());
    const calls = templates.findOne.mock.calls.length;
    await service.getEnrichedRows('org1', ids.parts.toString());

    expect(templates.findOne.mock.calls.length).toBe(calls);
  });
});
