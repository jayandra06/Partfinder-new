import { WmsSetupService } from './wms-setup.service';
import { WmsTenantCollectionsService } from './wms-tenant-collections.service';
import type { WmsRequestContext } from '../wms.types';

describe('WmsSetupService operator scope lists', () => {
  const ctxBase = (): WmsRequestContext => ({
    orgCode: '900100',
    organizationName: 'Staging',
    tenantUri: 'mongodb://test',
    email: 'op@test',
    role: 'Employee',
    isWmsAdmin: false,
    allowedWarehouseIds: ['wh-a'],
    allowedOwnerIds: ['own-a'],
    wmsEnabled: true,
    maxWmsWarehouses: 10,
    maxStockOwners: 50,
    operatorPermissions: {
      receiving: true,
      qc: true,
      putaway: true,
      supervise: false,
    },
  });

  function makeService(warehouseDocs: unknown[], ownerDocs: unknown[]) {
    const collections = {
      ensureIndexes: jest.fn().mockResolvedValue(undefined),
      warehouses: jest.fn().mockResolvedValue({
        find: () => ({
          sort: () => ({
            toArray: async () => warehouseDocs,
          }),
        }),
      }),
      stockOwners: jest.fn().mockResolvedValue({
        find: () => ({
          sort: () => ({
            toArray: async () => ownerDocs,
          }),
        }),
      }),
    };
    return new WmsSetupService(
      collections as unknown as WmsTenantCollectionsService,
    );
  }

  it('filters warehouses to granted ids for operators', async () => {
    const service = makeService(
      [
        { _id: 'wh-a', code: 'A', name: 'Allowed', isActive: true },
        { _id: 'wh-b', code: 'B', name: 'Hidden', isActive: true },
      ],
      [],
    );
    const result = await service.listWarehouses(ctxBase());
    expect(result.items.map((w) => w.id)).toEqual(['wh-a']);
  });

  it('filters stock owners to granted ids for operators', async () => {
    const service = makeService(
      [],
      [
        { _id: 'own-a', code: 'A', name: 'Client A', isActive: true },
        { _id: 'own-b', code: 'B', name: 'Client B', isActive: true },
      ],
    );
    const result = await service.listStockOwners(ctxBase());
    expect(result.items.map((o) => o.id)).toEqual(['own-a']);
  });
});