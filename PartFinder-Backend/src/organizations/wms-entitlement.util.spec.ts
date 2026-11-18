import { isWmsProductEntitled } from './wms-entitlement.util';

describe('isWmsProductEntitled', () => {
  it('is true only when wmsEnabled is explicitly true', () => {
    expect(isWmsProductEntitled({ wmsEnabled: true })).toBe(true);
  });

  it('is false when wmsEnabled is false, undefined, or null', () => {
    expect(isWmsProductEntitled({ wmsEnabled: false })).toBe(false);
    expect(isWmsProductEntitled({ wmsEnabled: undefined as unknown as boolean })).toBe(
      false,
    );
    expect(isWmsProductEntitled({ wmsEnabled: null as unknown as boolean })).toBe(
      false,
    );
  });
});