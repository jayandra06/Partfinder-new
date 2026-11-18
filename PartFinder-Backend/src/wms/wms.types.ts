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
  operatorPermissions: WmsOperatorGrants;
};

export const WMS_COLLECTIONS = {
  warehouses: 'wms_warehouses',
  stockOwners: 'wms_stock_owners',
  zones: 'wms_zones',
  locations: 'wms_locations',
  operatorGrants: 'wms_operator_grants',
  skus: 'wms_skus',
  skuIdentifiers: 'wms_sku_identifiers',
  skuAuditLog: 'wms_sku_audit_log',
  uomDefinitions: 'wms_uom_definitions',
  uomTemplateMappings: 'wms_uom_template_mappings',
  stockBalances: 'wms_stock_balances',
  stockTransactions: 'wms_stock_transactions',
  idempotencyKeys: 'wms_idempotency_keys',
  erpConnectors: 'wms_erp_connectors',
  erpSkuMappings: 'wms_erp_sku_mappings',
  erpLocationMappings: 'wms_erp_location_mappings',
  inboundMessages: 'wms_inbound_messages',
  outboxEvents: 'wms_outbox_events',
  expectedReceipts: 'wms_expected_receipts',
  goodsReceipts: 'wms_goods_receipts',
  receiptLines: 'wms_receipt_lines',
  qcInspections: 'wms_qc_inspections',
  putawayTasks: 'wms_putaway_tasks',
  receivingAuditLog: 'wms_receiving_audit_log',
  stagingGateEvidence: 'wms_staging_gate_evidence',
} as const;

export type WmsOperatorPermission =
  | 'receiving'
  | 'qc'
  | 'putaway'
  | 'supervise'
  | 'admin';

export type WmsOperatorGrants = {
  receiving: boolean;
  qc: boolean;
  putaway: boolean;
  supervise: boolean;
};

export type WmsSkuStatus = 'active' | 'merged' | 'retired';

export type WmsIdentifierStatus = 'active' | 'superseded';