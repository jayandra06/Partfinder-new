export type WmsBalanceKeyParts = {
  ownerId: string;
  warehouseId: string;
  locationId: string;
  skuKey: string;
  lotId?: string | null;
  serialId?: string | null;
};

export function buildBalanceKey(parts: WmsBalanceKeyParts): string {
  const lot = (parts.lotId ?? '').trim();
  const serial = (parts.serialId ?? '').trim();
  return [
    parts.ownerId,
    parts.warehouseId,
    parts.locationId,
    parts.skuKey,
    lot,
    serial,
  ].join('|');
}

/** Available-to-promise — never stored as an independent physical quantity. */
export function computeAtp(onHand: number, reserved: number, blockedQty: number): number {
  return onHand - reserved - blockedQty;
}