import { ForbiddenException } from '@nestjs/common';
import {
  assertOwnerAccess,
  assertWarehouseAccess,
  assertWmsAdminWrite,
} from './wms-scope.util';
import type { WmsRequestContext } from './wms.types';

const employeeCtx = (
  warehouseIds: string[],
  ownerIds: string[],
): WmsRequestContext => ({
  orgCode: '111111',
  organizationName: 'Test',
  tenantUri: 'mongodb://127.0.0.1/test',
  email: 'emp@b.com',
  role: 'Employee',
  isWmsAdmin: false,
  allowedWarehouseIds: warehouseIds,
  allowedOwnerIds: ownerIds,
  wmsEnabled: true,
  maxWmsWarehouses: 10,
  maxStockOwners: 50,
  operatorPermissions: {
    receiving: true,
    qc: false,
    putaway: false,
    supervise: false,
  },
});

describe('wms-scope.util', () => {
  it('denies warehouse outside employee grants', () => {
    const ctx = employeeCtx(['wh-a'], ['own-a']);
    expect(() => assertWarehouseAccess(ctx, 'wh-b')).toThrow(ForbiddenException);
  });

  it('denies stock owner outside employee grants', () => {
    const ctx = employeeCtx(['wh-a'], ['own-a']);
    expect(() => assertOwnerAccess(ctx, 'own-b')).toThrow(ForbiddenException);
  });

  it('denies write for non-admin', () => {
    const ctx = employeeCtx(['wh-a'], ['own-a']);
    expect(() => assertWmsAdminWrite(ctx)).toThrow(ForbiddenException);
  });
});