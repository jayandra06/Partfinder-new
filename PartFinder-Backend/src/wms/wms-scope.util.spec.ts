import { ForbiddenException } from '@nestjs/common';
import { assertWarehouseAccess, assertWmsAdminWrite } from './wms-scope.util';

describe('wms-scope.util', () => {
  it('denies warehouse outside allow list', () => {
    expect(() =>
      assertWarehouseAccess(
        { allowedWarehouseIds: ['w1'], allowedOwnerIds: 'all', isWmsAdmin: false },
        'w2',
      ),
    ).toThrow(ForbiddenException);
  });

  it('requires admin for write', () => {
    expect(() =>
      assertWmsAdminWrite({
        allowedWarehouseIds: 'all',
        allowedOwnerIds: 'all',
        isWmsAdmin: false,
      }),
    ).toThrow(ForbiddenException);
  });
});
