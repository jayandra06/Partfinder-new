import 'reflect-metadata';
import { ForbiddenException, NotFoundException } from '@nestjs/common';
import { CasbinService } from '../../casbin/casbin.service';
import { OrganizationsService } from '../../organizations/organizations.service';
import { PartsService } from '../../parts/parts.service';
import { RelationsService } from '../../relations/relations.service';
import { TemplatesService } from '../../templates/templates.service';
import { ViewDataService } from '../../view-data/view-data.service';
import { WmsContextResolverService } from '../../wms/services/wms-context-resolver.service';
import { WmsInventoryReadService } from '../../wms/services/wms-inventory-read.service';
import { WmsSetupService } from '../../wms/services/wms-setup.service';
import { WmsSkuService } from '../../wms/services/wms-sku.service';
import { AiToolAuthService } from './ai-tool-auth.service';
import { AiToolRegistryService } from './ai-tool-registry.service';
import { AI_TOOL_MAX_RECORDS, AiToolExecutionContext } from './ai-tool.types';

describe('AiToolRegistryService (Phase 5 Batch 1)', () => {
  let organizationsService: jest.Mocked<Pick<OrganizationsService, 'findByOrgCode'>>;
  let casbinService: jest.Mocked<Pick<CasbinService, 'checkPermission'>>;
  let templatesService: jest.Mocked<
    Pick<
      TemplatesService,
      'list' | 'getOne' | 'getRows' | 'findMissingValues' | 'findDuplicateRecords'
    >
  >;
  let viewDataService: jest.Mocked<Pick<ViewDataService, 'getEnrichedRows'>>;
  let relationsService: jest.Mocked<Pick<RelationsService, 'list' | 'getOne'>>;
  let partsService: jest.Mocked<Pick<PartsService, 'list' | 'getById'>>;
  let wmsContextResolver: jest.Mocked<
    Pick<WmsContextResolverService, 'resolveContext'>
  >;
  let wmsSetupService: jest.Mocked<Pick<WmsSetupService, 'listWarehouses'>>;
  let wmsInventoryReadService: jest.Mocked<
    Pick<WmsInventoryReadService, 'listBalances' | 'computeAtpForSku'>
  >;
  let wmsSkuService: jest.Mocked<
    Pick<WmsSkuService, 'getSku' | 'resolveSkuIdsByQuery'>
  >;
  let authService: AiToolAuthService;
  let registry: AiToolRegistryService;

  const baseContext: AiToolExecutionContext = {
    orgId: '884470',
    userId: 'user-1',
    email: 'engineer@partfinder.test',
    role: 'e',
    requestId: 'req-123',
  };

  beforeEach(() => {
    organizationsService = {
      findByOrgCode: jest.fn().mockResolvedValue({
        orgCode: '884470',
        orgDatabaseUri: 'mongodb://tenant-host/org_884470',
        licensePermanentlyBanned: false,
        licenseBannedUntil: null,
      } as never),
    };

    casbinService = {
      checkPermission: jest.fn().mockResolvedValue(true),
    };

    templatesService = {
      list: jest.fn().mockResolvedValue([]),
      getOne: jest.fn(),
      getRows: jest.fn(),
      findMissingValues: jest.fn(),
      findDuplicateRecords: jest.fn(),
    };

    viewDataService = {
      getEnrichedRows: jest.fn().mockResolvedValue([]),
    };

    relationsService = {
      list: jest.fn().mockResolvedValue([]),
      getOne: jest.fn(),
    };

    partsService = {
      list: jest.fn().mockResolvedValue({ page: 1, limit: 50, total: 0, items: [] }),
      getById: jest.fn(),
    };

    wmsContextResolver = {
      resolveContext: jest.fn().mockResolvedValue({
        orgCode: '884470',
        organizationName: 'Test Org',
        tenantUri: 'mongodb://tenant-host/org_884470',
        email: 'engineer@partfinder.test',
        role: 'Employee',
        isWmsAdmin: false,
        allowedWarehouseIds: ['wh-1'],
        allowedOwnerIds: ['own-1'],
        wmsEnabled: true,
        maxWmsWarehouses: 10,
        maxStockOwners: 50,
        operatorPermissions: {
          receiving: true,
          qc: true,
          putaway: true,
          supervise: false,
        },
      }),
    };

    wmsSetupService = {
      listWarehouses: jest.fn().mockResolvedValue({
        items: [
          {
            id: 'wh-1',
            code: 'WH-MAIN',
            name: 'Main Warehouse',
            address: '100 Industrial Way',
            timeZone: 'UTC',
            isActive: true,
          },
        ],
        total: 1,
      } as never),
    };

    wmsInventoryReadService = {
      listBalances: jest.fn().mockResolvedValue({
        items: [],
        total: 0,
        skip: 0,
        limit: 50,
      }),
      computeAtpForSku: jest.fn().mockResolvedValue({
        ownerId: 'own-1',
        warehouseId: 'wh-1',
        skuId: 'sku-1',
        onHand: 0,
        reserved: 0,
        blockedQty: 0,
        available: 0,
      }),
    };

    wmsSkuService = {
      getSku: jest.fn().mockResolvedValue({
        skuId: 'sku-1',
        displayName: 'Bearing 6205',
        primaryUom: 'EA',
      } as never),
      resolveSkuIdsByQuery: jest.fn().mockResolvedValue([
        { skuId: 'sku-1', displayName: 'Bearing 6205', primaryUom: 'EA' },
      ]),
    };

    authService = new AiToolAuthService(
      organizationsService as unknown as OrganizationsService,
      casbinService as unknown as CasbinService,
    );

    registry = new AiToolRegistryService(
      authService,
      templatesService as unknown as TemplatesService,
      viewDataService as unknown as ViewDataService,
      relationsService as unknown as RelationsService,
      partsService as unknown as PartsService,
      wmsContextResolver as unknown as WmsContextResolverService,
      wmsSetupService as unknown as WmsSetupService,
      wmsInventoryReadService as unknown as WmsInventoryReadService,
      wmsSkuService as unknown as WmsSkuService,
    );
  });

  it('registers all Batch 1, Batch 2, Batch 3A, and Batch 3B tools with expected limits', () => {
    const defs = registry.getToolDefinitions();
    expect(defs.map((d) => d.name)).toEqual([
      'get_template_schema',
      'search_template_rows',
      'get_template_relationships',
      'get_part_details',
      'compare_vendor_prices',
      'find_cheapest_vendor',
      'find_missing_values',
      'find_duplicate_records',
      'get_part_inventory',
      'find_part_warehouse',
      'get_warehouse_details',
      'check_stock_availability',
      'export_template_data',
      'export_vendor_comparison',
      'export_missing_values_report',
      'export_duplicate_records_report',
    ]);
    for (const def of defs) {
      expect(def.readOnly).toBe(true);
      expect(def.requiresConfirmation).toBe(false);
      const expectedMax =
        def.name === 'export_template_data' ||
        def.name === 'export_missing_values_report' ||
        def.name === 'export_duplicate_records_report'
          ? 500
          : AI_TOOL_MAX_RECORDS;
      expect(def.maxResultLimit).toBe(expectedMax);
    }
  });

  it('rejects unknown tools', async () => {
    const res = await registry.executeTool('delete_all_parts', {}, baseContext);
    expect(res.ok).toBe(false);
    expect(res.error?.code).toBe('UNKNOWN_TOOL');
  });

  it('rejects missing orgId for tenant isolation', async () => {
    const res = await registry.executeTool(
      'get_template_schema',
      {},
      { ...baseContext, orgId: '   ' },
    );
    expect(res.ok).toBe(false);
    expect(res.error?.code).toBe('TENANT_REQUIRED');
  });

  it('rejects unknown arguments and out-of-range limits', async () => {
    const unknownProp = await registry.executeTool(
      'get_template_schema',
      { templateId: 'tpl-1', sqlInject: 'DROP TABLE' },
      baseContext,
    );
    expect(unknownProp.ok).toBe(false);
    expect(unknownProp.error?.code).toBe('INVALID_ARGUMENTS');

    const overLimit = await registry.executeTool(
      'search_template_rows',
      { templateId: 'tpl-1', limit: 200 },
      baseContext,
    );
    expect(overLimit.ok).toBe(false);
    expect(overLimit.error?.code).toBe('INVALID_ARGUMENTS');

    const missingRequired = await registry.executeTool(
      'search_template_rows',
      { query: 'bearing' },
      baseContext,
    );
    expect(missingRequired.ok).toBe(false);
    expect(missingRequired.error?.code).toBe('INVALID_ARGUMENTS');
  });

  it('denies execution when Casbin rejects permission', async () => {
    casbinService.checkPermission.mockResolvedValue(false);

    const res = await registry.executeTool(
      'get_part_details',
      { search: 'M8' },
      baseContext,
    );

    expect(res.ok).toBe(false);
    expect(res.error?.code).toBe('FORBIDDEN');
    expect(casbinService.checkPermission).toHaveBeenCalledWith(
      'mongodb://tenant-host/org_884470',
      'e',
      'parts',
      'view',
    );
    expect(partsService.list).not.toHaveBeenCalled();
  });

  it('denies execution when organization license is banned', async () => {
    organizationsService.findByOrgCode.mockResolvedValue({
      orgCode: '884470',
      orgDatabaseUri: 'mongodb://tenant-host/org_884470',
      licensePermanentlyBanned: true,
    } as never);

    const res = await registry.executeTool('get_template_schema', {}, baseContext);
    expect(res.ok).toBe(false);
    expect(res.error?.code).toBe('FORBIDDEN');
  });

  it('executes get_template_schema for a specific template with tenant isolation', async () => {
    templatesService.getOne.mockResolvedValue({
      _id: 'tpl-1',
      name: 'Fasteners',
      columns: [
        { _id: 'c1', name: 'PartNumber', order: 0, type: 'text', isLink: false },
        { _id: 'c2', name: 'Vendor', order: 1, type: 'link', isLink: true },
      ],
    });

    const res = await registry.executeTool(
      'get_template_schema',
      { templateId: 'tpl-1' },
      baseContext,
    );

    expect(res.ok).toBe(true);
    expect(res.dataSource).toBe('central_mongodb');
    expect(templatesService.getOne).toHaveBeenCalledWith('884470', 'tpl-1');
    expect(res.data).toEqual({
      template: {
        id: 'tpl-1',
        name: 'Fasteners',
        columnCount: 2,
        columns: [
          { id: 'c1', name: 'PartNumber', order: 0, type: 'text', isLink: false },
          { id: 'c2', name: 'Vendor', order: 1, type: 'link', isLink: true },
        ],
      },
    });
  });

  it('executes search_template_rows with query filtering and enriched view data', async () => {
    viewDataService.getEnrichedRows.mockResolvedValue([
      {
        rowId: 'r1',
        cells: { PartNumber: 'SKF-6205', Category: 'Bearing' },
        linkedData: { VendorRel: [{ Name: 'SKF' }] },
      },
      {
        rowId: 'r2',
        cells: { PartNumber: 'DIN-933', Category: 'Bolt' },
        linkedData: {},
      },
    ]);

    const res = await registry.executeTool(
      'search_template_rows',
      {
        templateId: 'tpl-1',
        query: '6205',
        columnFilter: 'PartNumber',
        includeLinkedData: true,
      },
      baseContext,
    );

    expect(res.ok).toBe(true);
    expect(res.returnedCount).toBe(1);
    expect(viewDataService.getEnrichedRows).toHaveBeenCalledWith('884470', 'tpl-1');
    expect((res.data as { rows: unknown[] }).rows).toHaveLength(1);
  });

  it('caps large results at 50 records and marks truncated', async () => {
    const manyTemplates = Array.from({ length: 65 }, (_, i) => ({
      _id: `tpl-${i}`,
      name: `Template ${i}`,
    }));
    templatesService.list.mockResolvedValue(manyTemplates as never);

    const res = await registry.executeTool('get_template_schema', {}, baseContext);

    expect(res.ok).toBe(true);
    expect(res.returnedCount).toBe(50);
    expect(res.truncated).toBe(true);
    expect((res.data as { templates: unknown[] }).templates).toHaveLength(50);
  });

  it('executes get_template_relationships filtered by templateId', async () => {
    relationsService.list.mockResolvedValue([
      {
        _id: 'rel-1',
        name: 'Vendor Lookup',
        primaryTemplateId: 'tpl-1',
        lookupTemplateId: 'tpl-2',
        triggerColumn: 'VendorCode',
        menuLabel: 'View Vendor',
        matchKeys: [{ sourceColumn: 'VendorCode', targetColumn: 'Code' }],
        displayColumns: ['VendorName', 'Price'],
      },
      {
        _id: 'rel-2',
        name: 'Other',
        primaryTemplateId: 'tpl-3',
        lookupTemplateId: 'tpl-4',
        triggerColumn: 'X',
        menuLabel: 'X',
        matchKeys: [],
        displayColumns: [],
      },
    ] as never);

    const res = await registry.executeTool(
      'get_template_relationships',
      { templateId: 'tpl-1' },
      baseContext,
    );

    expect(res.ok).toBe(true);
    expect(res.returnedCount).toBe(1);
    expect((res.data as { relationships: Array<{ id: string }> }).relationships[0].id).toBe(
      'rel-1',
    );
  });

  it('executes get_part_details for a single partId', async () => {
    partsService.getById.mockResolvedValue({
      _id: 'part-1',
      templateId: 'tpl-1',
      rowId: 'row-9',
      metadata: { PartNumber: 'PN-100', Price: '12.50' },
    } as never);

    const res = await registry.executeTool(
      'get_part_details',
      { partId: 'part-1' },
      baseContext,
    );

    expect(res.ok).toBe(true);
    expect(partsService.getById).toHaveBeenCalledWith('884470', 'part-1');
    expect((res.data as { part: { id: string } }).part.id).toBe('part-1');
  });

  it('reports NOT_FOUND and tenantDatabaseNote when central DB misses and tenant DB is configured', async () => {
    templatesService.getOne.mockRejectedValue(
      new NotFoundException('Template not found'),
    );

    const res = await registry.executeTool(
      'get_template_schema',
      { templateId: 'missing-id' },
      baseContext,
    );

    expect(res.ok).toBe(false);
    expect(res.error?.code).toBe('NOT_FOUND');
    expect(res.tenantDatabaseNote).toContain('partfinder_templates');
  });

  it('includes tenantDatabaseNote when central parts list is empty and tenant DB is configured', async () => {
    const res = await registry.executeTool(
      'get_part_details',
      { search: 'bearing' },
      baseContext,
    );

    expect(res.ok).toBe(true);
    expect(res.returnedCount).toBe(0);
    expect(res.tenantDatabaseNote).toContain('partfinder_master_data_rows');
  });

  describe('Batch 2 tools', () => {
    it('compares vendor prices across enriched rows and ignores invalid prices', async () => {
      templatesService.list.mockResolvedValue([
        { _id: 'tpl-1', name: 'Master Parts' },
      ] as never);
      viewDataService.getEnrichedRows.mockResolvedValue([
        {
          rowId: 'r1',
          cells: { PartNumber: 'P1001', Vendor: 'VendorA', Price: '$15.50' },
          linkedData: {
            AltVendors: [
              { VendorName: 'VendorB', UnitPrice: '$12.00' },
              { VendorName: 'VendorC', UnitPrice: 'TBD' },
            ],
          },
        },
      ]);

      const res = await registry.executeTool(
        'compare_vendor_prices',
        { partIdentifier: 'P1001' },
        baseContext,
      );

      expect(res.ok).toBe(true);
      expect(res.dataSource).toBe('central_mongodb');
      const data = res.data as {
        comparable: boolean;
        currency: string;
        lowestPrice: number;
        highestPrice: number;
        vendorsCompared: number;
        ignoredInvalidCount: number;
        offers: Array<{ vendor: string; price: number }>;
      };
      expect(data.comparable).toBe(true);
      expect(data.currency).toBe('USD');
      expect(data.lowestPrice).toBe(12);
      expect(data.highestPrice).toBe(15.5);
      expect(data.vendorsCompared).toBe(2);
      expect(data.ignoredInvalidCount).toBe(1);
      expect(data.offers.map((o) => o.vendor)).toEqual(['VendorB', 'VendorA']);
    });

    it('refuses cross-currency min/max comparison when currencies are incompatible', async () => {
      templatesService.list.mockResolvedValue([
        { _id: 'tpl-1', name: 'Master Parts' },
      ] as never);
      viewDataService.getEnrichedRows.mockResolvedValue([
        {
          rowId: 'r1',
          cells: { PartNumber: 'P1001', Vendor: 'US Vendor', Price: '$10.00' },
          linkedData: {},
        },
        {
          rowId: 'r2',
          cells: { PartNumber: 'P1001', Vendor: 'EU Vendor', Price: '€9.50' },
          linkedData: {},
        },
      ]);

      const res = await registry.executeTool(
        'compare_vendor_prices',
        { partIdentifier: 'P1001' },
        baseContext,
      );

      expect(res.ok).toBe(true);
      const data = res.data as {
        comparable: boolean;
        currency: string;
        lowestPrice: number | null;
        highestPrice: number | null;
        currenciesFound: string[];
      };
      expect(data.comparable).toBe(false);
      expect(data.currency).toBe('MIXED');
      expect(data.currenciesFound).toEqual(['USD', 'EUR']);
      expect(data.lowestPrice).toBeNull();
      expect(data.highestPrice).toBeNull();
    });

    it('finds cheapest vendor and alternative offers for a comparable currency', async () => {
      templatesService.list.mockResolvedValue([
        { _id: 'tpl-1', name: 'Vendors' },
      ] as never);
      viewDataService.getEnrichedRows.mockResolvedValue([
        {
          rowId: 'r1',
          cells: { PartNumber: 'P1001', Vendor: 'Alpha', Price: '25.00', Currency: 'USD' },
          linkedData: {},
        },
        {
          rowId: 'r2',
          cells: { PartNumber: 'P1001', Vendor: 'Beta', Price: '19.50', Currency: 'USD' },
          linkedData: {},
        },
        {
          rowId: 'r3',
          cells: { PartNumber: 'P1001', Vendor: 'Gamma', Price: '22.00', Currency: 'USD' },
          linkedData: {},
        },
      ]);

      const res = await registry.executeTool(
        'find_cheapest_vendor',
        { partIdentifier: 'P1001', limit: 2 },
        baseContext,
      );

      expect(res.ok).toBe(true);
      const data = res.data as {
        comparable: boolean;
        cheapestVendor: { vendor: string; price: number };
        lowestPrice: number;
        alternatives: Array<{ vendor: string; price: number }>;
        vendorsCompared: number;
      };
      expect(data.comparable).toBe(true);
      expect(data.cheapestVendor.vendor).toBe('Beta');
      expect(data.lowestPrice).toBe(19.5);
      expect(data.alternatives.map((a) => a.vendor)).toEqual(['Gamma']);
      expect(res.returnedCount).toBe(2);
      expect(data.vendorsCompared).toBe(3);
    });

    it('enforces the 50-record total cap in find_cheapest_vendor when limit is 50', async () => {
      templatesService.list.mockResolvedValue([
        { _id: 'tpl-1', name: 'Vendors' },
      ] as never);
      const sixtyOffers = Array.from({ length: 60 }, (_, idx) => ({
        rowId: `r${idx}`,
        cells: {
          PartNumber: 'P1001',
          Vendor: `Vendor-${String(idx).padStart(2, '0')}`,
          Price: String(10 + idx),
          Currency: 'USD',
        },
        linkedData: {},
      }));
      // Include a tied cheapest price
      sixtyOffers[59].cells.Price = '10';
      viewDataService.getEnrichedRows.mockResolvedValue(sixtyOffers);

      const res = await registry.executeTool(
        'find_cheapest_vendor',
        { partIdentifier: 'P1001', limit: 50 },
        baseContext,
      );

      expect(res.ok).toBe(true);
      expect(res.returnedCount).toBe(50);
      expect(res.truncated).toBe(true);
      const data = res.data as {
        cheapestVendor: { vendor: string; price: number };
        alternatives: Array<{ vendor: string; price: number }>;
        vendorsCompared: number;
      };
      expect(data.cheapestVendor).toEqual(
        expect.objectContaining({ vendor: 'Vendor-00', price: 10 }),
      );
      expect(data.alternatives[0]).toEqual(
        expect.objectContaining({ vendor: 'Vendor-59', price: 10 }),
      );
      expect(data.alternatives).toHaveLength(49);
      expect(data.vendorsCompared).toBe(60);
    });

    it('executes find_missing_values with column validation and tenant isolation', async () => {
      templatesService.findMissingValues.mockResolvedValue({
        templateId: 'tpl-1',
        templateName: 'Fasteners',
        columnsAnalyzed: [{ id: 'c1', name: 'Vendor' }],
        missingCountsByColumn: { Vendor: 2 },
        totalAffectedRows: 2,
        page: 1,
        limit: 50,
        rows: [
          { rowId: 'r1', missingColumns: ['Vendor'], cells: { Vendor: '' } },
          { rowId: 'r2', missingColumns: ['Vendor'], cells: { Vendor: '   ' } },
        ],
      });

      const res = await registry.executeTool(
        'find_missing_values',
        { templateId: 'tpl-1', columnNames: ['Vendor'] },
        baseContext,
      );

      expect(res.ok).toBe(true);
      expect(templatesService.findMissingValues).toHaveBeenCalledWith(
        '884470',
        'tpl-1',
        expect.objectContaining({ columnNames: ['Vendor'] }),
      );
      expect(res.returnedCount).toBe(2);
    });

    it('executes find_duplicate_records and rejects invalid arguments', async () => {
      templatesService.findDuplicateRecords.mockResolvedValue({
        templateId: 'tpl-1',
        templateName: 'Fasteners',
        keyColumns: [{ id: 'c1', name: 'PartNumber' }],
        totalDuplicateGroups: 1,
        page: 1,
        limit: 50,
        duplicateGroups: [
          {
            keyValues: { PartNumber: 'p1001' },
            duplicateCount: 2,
            rowIds: ['r1', 'r4'],
          },
        ],
      });

      const res = await registry.executeTool(
        'find_duplicate_records',
        { templateId: 'tpl-1', columnNames: ['PartNumber'] },
        baseContext,
      );

      expect(res.ok).toBe(true);
      expect(res.returnedCount).toBe(1);
      expect(templatesService.findDuplicateRecords).toHaveBeenCalledWith(
        '884470',
        'tpl-1',
        expect.objectContaining({ columnNames: ['PartNumber'] }),
      );

      const invalid = await registry.executeTool(
        'find_duplicate_records',
        { templateId: 'tpl-1', unknownFlag: true },
        baseContext,
      );
      expect(invalid.ok).toBe(false);
      expect(invalid.error?.code).toBe('INVALID_ARGUMENTS');
    });
  });

  describe('Batch 3A WMS tools', () => {
    it('executes get_part_inventory with tenant_mongodb_wms source and totals', async () => {
      wmsInventoryReadService.listBalances.mockResolvedValue({
        items: [
          {
            balanceKey: 'bk-1',
            ownerId: 'own-1',
            warehouseId: 'wh-1',
            locationId: 'loc-A1',
            skuId: 'sku-1',
            lotId: null,
            serialId: null,
            primaryUom: 'EA',
            onHand: 100,
            reserved: 25,
            blockedQty: 5,
            available: 70,
            version: 1,
            updatedAt: '2026-11-27T00:00:00Z',
          },
        ],
        total: 1,
        skip: 0,
        limit: 50,
      });

      const res = await registry.executeTool(
        'get_part_inventory',
        { partIdentifier: '6205' },
        baseContext,
      );

      expect(res.ok).toBe(true);
      expect(res.dataSource).toBe('tenant_mongodb_wms');
      expect(wmsContextResolver.resolveContext).toHaveBeenCalledWith(
        '884470',
        'engineer@partfinder.test',
      );
      const data = res.data as {
        totals: { onHand: number; reserved: number; available: number };
        balances: Array<{ warehouseCode: string }>;
      };
      expect(data.totals).toEqual({
        onHand: 100,
        reserved: 25,
        blockedQty: 5,
        available: 70,
      });
      expect(data.balances[0].warehouseCode).toBe('WH-MAIN');
    });

    it('executes find_part_warehouse and filters by onlyAvailable', async () => {
      wmsInventoryReadService.listBalances.mockResolvedValue({
        items: [
          {
            balanceKey: 'bk-1',
            ownerId: 'own-1',
            warehouseId: 'wh-1',
            locationId: 'loc-A1',
            skuId: 'sku-1',
            lotId: null,
            serialId: null,
            primaryUom: 'EA',
            onHand: 10,
            reserved: 10,
            blockedQty: 0,
            available: 0,
            version: 1,
            updatedAt: null,
          },
        ],
        total: 1,
        skip: 0,
        limit: 50,
      });

      const res = await registry.executeTool(
        'find_part_warehouse',
        { skuId: 'sku-1', onlyAvailable: true },
        baseContext,
      );

      expect(res.ok).toBe(true);
      expect(res.returnedCount).toBe(0);
    });

    it('executes get_warehouse_details for permitted warehouses', async () => {
      const res = await registry.executeTool(
        'get_warehouse_details',
        { warehouseId: 'WH-MAIN' },
        baseContext,
      );

      expect(res.ok).toBe(true);
      expect(res.dataSource).toBe('tenant_mongodb_wms');
      expect(res.returnedCount).toBe(1);
    });

    it('executes check_stock_availability and compares requiredQuantity against ATP', async () => {
      wmsInventoryReadService.computeAtpForSku.mockResolvedValue({
        ownerId: 'own-1',
        warehouseId: 'wh-1',
        skuId: 'sku-1',
        onHand: 50,
        reserved: 10,
        blockedQty: 0,
        available: 40,
      });

      const res = await registry.executeTool(
        'check_stock_availability',
        {
          skuId: 'sku-1',
          warehouseId: 'wh-1',
          ownerId: 'own-1',
          requiredQuantity: 35,
        },
        baseContext,
      );

      expect(res.ok).toBe(true);
      const data = res.data as {
        available: number;
        sufficientForRequired: boolean;
      };
      expect(data.available).toBe(40);
      expect(data.sufficientForRequired).toBe(true);
    });

    it('enforces WMS operator grant and entitlement denials as FORBIDDEN', async () => {
      wmsContextResolver.resolveContext.mockRejectedValue(
        new ForbiddenException('No WMS operator grants assigned.'),
      );

      const res = await registry.executeTool(
        'get_warehouse_details',
        {},
        baseContext,
      );

      expect(res.ok).toBe(false);
      expect(res.error?.code).toBe('FORBIDDEN');
      expect(res.error?.message).toContain('No WMS operator grants');
    });

    it('rejects WMS inventory query when neither skuId nor partIdentifier is provided', async () => {
      const res = await registry.executeTool(
        'get_part_inventory',
        {},
        baseContext,
      );

      expect(res.ok).toBe(false);
      expect(res.error?.code).toBe('INVALID_ARGUMENTS');
    });
  });

  describe('Batch 3B Export tools', () => {
    it('exports template data as formula-safe CSV and neutralizes formula injection', async () => {
      templatesService.getOne.mockResolvedValue({
        _id: 'tpl-1',
        name: 'Parts Sheet',
        columns: [
          { _id: 'c1', name: 'PartNumber', order: 0 },
          { _id: 'c2', name: 'Notes', order: 1 },
        ],
      });
      templatesService.getRows.mockResolvedValue({
        total: 2,
        rows: [
          { _id: 'r1', cells: { PartNumber: 'P1001', Notes: '=CMD("calc")' } },
          { _id: 'r2', cells: { PartNumber: '+123', Notes: '@SUM(A1)' } },
        ],
      } as never);

      const res = await registry.executeTool(
        'export_template_data',
        { templateId: 'tpl-1', format: 'csv' },
        baseContext,
      );

      expect(res.ok).toBe(true);
      const data = res.data as {
        exportType: string;
        fileNameSuggestion: string;
        csvContent: string;
        rowCount: number;
      };
      expect(data.exportType).toBe('template_data');
      expect(data.fileNameSuggestion).toBe('template-parts-sheet.csv');
      expect(data.rowCount).toBe(2);
      expect(data.csvContent).toContain("'=CMD(");
      expect(data.csvContent).toContain("'+123");
      expect(data.csvContent).toContain("'@SUM(A1)");
    });

    it('rejects export_template_data limit above 500', async () => {
      const res = await registry.executeTool(
        'export_template_data',
        { templateId: 'tpl-1', limit: 501 },
        baseContext,
      );

      expect(res.ok).toBe(false);
      expect(res.error?.code).toBe('INVALID_ARGUMENTS');
    });

    it('exports vendor comparison preserving currency comparability metadata', async () => {
      templatesService.list.mockResolvedValue([
        { _id: 'tpl-1', name: 'Vendors' },
      ] as never);
      viewDataService.getEnrichedRows.mockResolvedValue([
        {
          rowId: 'r1',
          cells: { PartNumber: 'P1001', Vendor: 'US', Price: '$10' },
          linkedData: {},
        },
        {
          rowId: 'r2',
          cells: { PartNumber: 'P1001', Vendor: 'EU', Price: '€9' },
          linkedData: {},
        },
      ]);

      const res = await registry.executeTool(
        'export_vendor_comparison',
        { partIdentifier: 'P1001', format: 'json_table' },
        baseContext,
      );

      expect(res.ok).toBe(true);
      const data = res.data as {
        fileNameSuggestion: string;
        metadata: { comparable: boolean; currency: string; lowestPrice: null };
        rowCount: number;
      };
      expect(data.fileNameSuggestion).toBe('vendor-comparison-p1001.xlsx');
      expect(data.rowCount).toBe(2);
      expect(data.metadata.comparable).toBe(false);
      expect(data.metadata.currency).toBe('MIXED');
      expect(data.metadata.lowestPrice).toBeNull();
    });

    it('exports missing values report and duplicate records report', async () => {
      templatesService.findMissingValues.mockResolvedValue({
        templateId: 'tpl-1',
        templateName: 'Fasteners',
        columnsAnalyzed: [{ id: 'c1', name: 'Vendor' }],
        missingCountsByColumn: { Vendor: 1 },
        totalAffectedRows: 1,
        page: 1,
        limit: 500,
        rows: [{ rowId: 'r1', missingColumns: ['Vendor'], cells: { Vendor: '' } }],
      });

      const missingRes = await registry.executeTool(
        'export_missing_values_report',
        { templateId: 'tpl-1', columnNames: ['Vendor'] },
        baseContext,
      );
      expect(missingRes.ok).toBe(true);
      expect((missingRes.data as { rowCount: number }).rowCount).toBe(1);

      templatesService.findDuplicateRecords.mockResolvedValue({
        templateId: 'tpl-1',
        templateName: 'Fasteners',
        keyColumns: [{ id: 'c1', name: 'PartNumber' }],
        totalDuplicateGroups: 1,
        page: 1,
        limit: 500,
        duplicateGroups: [
          { keyValues: { PartNumber: 'P1001' }, duplicateCount: 2, rowIds: ['r1', 'r2'] },
        ],
      });

      const dupRes = await registry.executeTool(
        'export_duplicate_records_report',
        { templateId: 'tpl-1', columnNames: ['PartNumber'] },
        baseContext,
      );
      expect(dupRes.ok).toBe(true);
      expect((dupRes.data as { rowCount: number }).rowCount).toBe(1);
    });
  });
});
