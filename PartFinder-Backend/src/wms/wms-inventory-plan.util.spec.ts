import { sumMoveQuantity } from './wms-inventory-plan.util';

describe('move conservation', () => {
  it('sums quantities moved between locations', () => {
    const total = sumMoveQuantity([
      { skuId: 'A', quantity: 20, fromLocationId: 'R1', toLocationId: 'S1' },
    ]);
    expect(total).toBe(20);
  });
});