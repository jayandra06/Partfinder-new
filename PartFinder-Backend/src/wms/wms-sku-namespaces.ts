/** Identifier namespaces — uniqueness rules are per namespace + metadata scope. */
export const WMS_SKU_NAMESPACES = [
  'masterDataRow',
  'partsRecord',
  'manufacturerPn',
  'supplierPn',
  'erpMaterial',
  'barcode',
  'legacyInventory',
  'ownerExternalProduct',
] as const;

export type WmsSkuNamespace = (typeof WMS_SKU_NAMESPACES)[number];

export function parseNamespace(raw: string): WmsSkuNamespace | null {
  const n = raw.trim();
  return (WMS_SKU_NAMESPACES as readonly string[]).includes(n)
    ? (n as WmsSkuNamespace)
    : null;
}

/**
 * Scope key used for uniqueness checks among active identifiers.
 * Same part number in different namespaces / scopes may coexist.
 */
export function identifierScopeKey(
  namespace: WmsSkuNamespace,
  value: string,
  metadata: Record<string, unknown>,
): string {
  const v = value.trim().toUpperCase();
  switch (namespace) {
    case 'masterDataRow':
      return `md:${String(metadata.templateId ?? '')}:${String(metadata.rowId ?? v)}`;
    case 'partsRecord':
      return `pr:${String(metadata.templateId ?? '')}:${String(metadata.rowId ?? v)}`;
    case 'manufacturerPn':
      return `mpn:${String(metadata.manufacturerName ?? '').toUpperCase()}:${v}`;
    case 'supplierPn':
      return `spn:${String(metadata.supplierId ?? metadata.supplierName ?? '').toUpperCase()}:${v}`;
    case 'erpMaterial':
      return `erp:${String(metadata.erpConnectorId ?? '')}:${v}`;
    case 'barcode':
      return `bc:${String(metadata.symbology ?? 'code128').toUpperCase()}:${v}`;
    case 'legacyInventory':
      return `leg:${v}`;
    case 'ownerExternalProduct':
      return `own:${String(metadata.ownerId ?? '')}:${v}`;
    default:
      return `${namespace}:${v}`;
  }
}