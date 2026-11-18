export const WMS_STOCK_TXN_TYPES = [
  'openingBalance',
  'receive',
  'move',
  'adjust',
  'writeOff',
  'reversal',
  'ownerTransfer',
  'qcBlock',
  'qcRelease',
] as const;

export type WmsStockTxnType = (typeof WMS_STOCK_TXN_TYPES)[number];

export type WmsInventoryLine = {
  skuId: string;
  quantity: number;
  fromLocationId?: string;
  toLocationId?: string;
  lotId?: string;
  serialId?: string;
  targetOwnerId?: string;
  uom?: string;
  qtyDelta?: number;
};

export type WmsInventoryPostCommand = {
  type: WmsStockTxnType;
  idempotencyKey: string;
  ownerId: string;
  warehouseId: string;
  lines: WmsInventoryLine[];
  refs?: Record<string, string>;
  reversalOf?: string;
  approvalNote?: string;
  emitErpEvent?: boolean;
};