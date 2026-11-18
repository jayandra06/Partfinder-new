import type { WmsInventoryLine, WmsStockTxnType } from './wms-stock-transaction.types';

/** Sum of physical on-hand deltas across locations for a move must net to zero. */
export function assertMoveConservesPhysical(
  type: WmsStockTxnType,
  lines: WmsInventoryLine[],
): void {
  if (type !== 'move') return;
  for (const line of lines) {
    if (!line.fromLocationId || !line.toLocationId || line.quantity <= 0) {
      throw new Error('Invalid move line');
    }
  }
}

export function sumMoveQuantity(lines: WmsInventoryLine[]): number {
  return lines.reduce((acc, l) => acc + (l.quantity > 0 ? l.quantity : 0), 0);
}