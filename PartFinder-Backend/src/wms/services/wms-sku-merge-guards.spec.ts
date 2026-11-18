import { BadRequestException } from '@nestjs/common';
import { WmsSkuService } from './wms-sku.service';

describe('WmsSkuService merge guards', () => {
  const ctx = {
    orgCode: 't',
    organizationName: 'T',
    tenantUri: 'mongodb://x',
    email: 'a@t.com',
    role: 'Admin' as const,
    isWmsAdmin: true,
    allowedWarehouseIds: 'all' as const,
    allowedOwnerIds: 'all' as const,
    wmsEnabled: true,
    maxWmsWarehouses: 10,
    maxStockOwners: 10,
  };

  function makeService(hasStock: boolean, txnRefs: number) {
    const collections = {
      ensurePhase02Indexes: jest.fn(),
      skus: jest.fn(),
      skuIdentifiers: jest.fn(),
    };
    const audit = { append: jest.fn() };
    const uom = { assertUomExists: jest.fn() };
    const inventoryRead = {
      hasStockForSku: jest.fn().mockResolvedValue(hasStock),
      countTxnReferences: jest.fn().mockResolvedValue(txnRefs),
    };
    const svc = new WmsSkuService(
      collections as never,
      audit as never,
      uom as never,
      inventoryRead as never,
    );
    jest
      .spyOn(WmsSkuService.prototype as unknown as { findSku: () => Promise<unknown> }, 'findSku')
      .mockImplementation(async (_c, id: string) => ({
        skuId: id,
        status: 'active',
        variantGroupId: 'g1',
      }));
    return { svc, collections };
  }

  it('rejects merge when source SKU has stock', async () => {
    const { svc } = makeService(true, 0);
    await expect(
      svc.mergeSku(ctx, 'SRC', { mergedIntoSkuId: 'TGT', reason: 'test' }),
    ).rejects.toBeInstanceOf(BadRequestException);
  });

  it('requires merge reason when ledger references exist', async () => {
    const { svc } = makeService(false, 2);
    await expect(
      svc.mergeSku(ctx, 'SRC', { mergedIntoSkuId: 'TGT' }),
    ).rejects.toBeInstanceOf(BadRequestException);
  });
});