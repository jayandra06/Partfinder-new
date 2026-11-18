/** Base stock UOM codes (ISO-style short codes). */
export const WMS_STANDARD_UOM = [
  { code: 'EA', name: 'Each / piece', category: 'count' },
  { code: 'BOX', name: 'Box / carton / pack', category: 'count' },
  { code: 'KG', name: 'Kilogram', category: 'mass' },
  { code: 'G', name: 'Gram', category: 'mass' },
  { code: 'M', name: 'Meter', category: 'length' },
  { code: 'CM', name: 'Centimeter', category: 'length' },
  { code: 'L', name: 'Liter', category: 'volume' },
  { code: 'ML', name: 'Milliliter', category: 'volume' },
] as const;

export type WmsUomCategory = 'count' | 'mass' | 'length' | 'volume' | 'custom';

export function isValidUomCode(code: string): boolean {
  const c = code.trim().toUpperCase();
  if (c.length < 1 || c.length > 16) {
    return false;
  }
  return /^[A-Z0-9_-]+$/.test(c);
}