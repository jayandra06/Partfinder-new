import { identifierScopeKey } from './wms-sku-namespaces';

describe('identifierScopeKey', () => {
  it('scopes manufacturer PNs by manufacturer name', () => {
    const a = identifierScopeKey('manufacturerPn', 'ABC-123', {
      manufacturerName: 'Acme',
    });
    const b = identifierScopeKey('manufacturerPn', 'abc-123', {
      manufacturerName: 'Other',
    });
    expect(a).toBe('mpn:ACME:ABC-123');
    expect(b).toBe('mpn:OTHER:ABC-123');
    expect(a).not.toBe(b);
  });

  it('scopes owner external products by ownerId', () => {
    const key = identifierScopeKey('ownerExternalProduct', 'SKU-9', {
      ownerId: 'owner-1',
    });
    expect(key).toBe('own:owner-1:SKU-9');
  });

  it('does not collapse different master data rows', () => {
    const k1 = identifierScopeKey('masterDataRow', 'x', {
      templateId: 't1',
      rowId: '1',
    });
    const k2 = identifierScopeKey('masterDataRow', 'x', {
      templateId: 't1',
      rowId: '2',
    });
    expect(k1).not.toBe(k2);
  });

  it('scopes ERP material by connector', () => {
    const k1 = identifierScopeKey('erpMaterial', 'MAT1', {
      erpConnectorId: 'erp-a',
    });
    const k2 = identifierScopeKey('erpMaterial', 'MAT1', {
      erpConnectorId: 'erp-b',
    });
    expect(k1).not.toBe(k2);
  });
});