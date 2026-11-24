export type WmsRole = 'Admin' | 'Employee';

export type WmsRequestContext = {
  orgCode: string;
  organizationName: string;
  tenantUri: string;
  email: string;
  role: WmsRole;
  isWmsAdmin: boolean;
  allowedWarehouseIds: string[] | 'all';
  allowedOwnerIds: string[] | 'all';
  wmsEnabled: boolean;
  maxWmsWarehouses: number;
  maxStockOwners: number;
};

export const WMS_COLLECTIONS = {
  warehouses: 'wms_warehouses',
  stockOwners: 'wms_stock_owners',
  zones: 'wms_zones',
  locations: 'wms_locations',
  operatorGrants: 'wms_operator_grants',
} as const;