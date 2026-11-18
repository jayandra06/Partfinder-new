import { buildBalanceKey, computeAtp } from './wms-balance-key.util';

describe('computeAtp', () => {
  it('never treats available as independent physical stock', () => {
    expect(computeAtp(100, 20, 0)).toBe(80);
    expect(computeAtp(100, 20, 5)).toBe(75);
  });
});

describe('buildBalanceKey', () => {
  it('separates lot and serial identity', () => {
    const a = buildBalanceKey({
      ownerId: 'o1',
      warehouseId: 'w1',
      locationId: 'l1',
      skuKey: 'SKU1',
      lotId: 'L1',
    });
    const b = buildBalanceKey({
      ownerId: 'o1',
      warehouseId: 'w1',
      locationId: 'l1',
      skuKey: 'SKU1',
      lotId: 'L2',
    });
    expect(a).not.toBe(b);
  });
});