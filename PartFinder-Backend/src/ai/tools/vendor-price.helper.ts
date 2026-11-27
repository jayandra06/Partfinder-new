import { PartsService } from '../../parts/parts.service';
import { TemplatesService } from '../../templates/templates.service';
import { ViewDataService } from '../../view-data/view-data.service';
import { AI_TOOL_MAX_RECORDS } from './ai-tool.types';

export interface VendorPriceOffer {
  vendor: string;
  price: number;
  rawPrice: string;
  currency: string;
  sourceTemplateId: string;
  sourceTemplateName: string;
  rowId: string;
}

export interface VendorPriceExtractionSummary {
  partIdentifier: string;
  offers: VendorPriceOffer[];
  ignoredInvalidCount: number;
  comparable: boolean;
  currenciesFound: string[];
  currency: string;
  lowestPrice: number | null;
  highestPrice: number | null;
  cheapestVendor: VendorPriceOffer | null;
  sourceTemplates: Array<{ id: string; name: string }>;
}

const PART_COLUMN_CANDIDATES = [
  'partnumber',
  'part number',
  'part_number',
  'partno',
  'part no',
  'part_no',
  'partid',
  'part id',
  'part_id',
  'sku',
  'mpn',
  'itemcode',
  'item code',
];

const VENDOR_COLUMN_CANDIDATES = [
  'vendor',
  'vendorname',
  'vendor name',
  'vendor_name',
  'supplier',
  'suppliername',
  'supplier name',
  'manufacturer',
];

const PRICE_COLUMN_CANDIDATES = [
  'price',
  'unitprice',
  'unit price',
  'unit_price',
  'vendorprice',
  'vendor price',
  'cost',
  'unitcost',
  'unit cost',
  'rate',
];

const CURRENCY_COLUMN_CANDIDATES = ['currency', 'curr', 'currencycode', 'currency code'];

const CURRENCY_SYMBOL_MAP: Array<[RegExp, string]> = [
  [/^\s*USD\b|\bUSD\s*$/i, 'USD'],
  [/^\s*EUR\b|\bEUR\s*$/i, 'EUR'],
  [/^\s*GBP\b|\bGBP\s*$/i, 'GBP'],
  [/^\s*INR\b|\bINR\s*$/i, 'INR'],
  [/^\s*JPY\b|\bJPY\s*$/i, 'JPY'],
  [/₹/, 'INR'],
  [/€/, 'EUR'],
  [/£/, 'GBP'],
  [/¥/, 'JPY'],
  [/\$/, 'USD'],
];

export async function extractVendorPriceOffers(
  deps: {
    templatesService: TemplatesService;
    viewDataService: ViewDataService;
    partsService: PartsService;
  },
  orgId: string,
  options: {
    partIdentifier: string;
    templateId?: string;
    partColumn?: string;
    vendorColumn?: string;
    priceColumn?: string;
    currency?: string;
  },
): Promise<VendorPriceExtractionSummary> {
  const targetPart = options.partIdentifier.trim().toLowerCase();
  const requestedCurrency = options.currency?.trim().toUpperCase();

  const templates = options.templateId
    ? [await deps.templatesService.getOne(orgId, options.templateId)]
    : await deps.templatesService.list(orgId);

  const templateNameById = new Map<string, string>();
  for (const t of templates) {
    const rec = t as Record<string, unknown>;
    templateNameById.set(String(rec._id ?? ''), String(rec.name ?? ''));
  }

  const offers: VendorPriceOffer[] = [];
  let ignoredInvalidCount = 0;

  for (const tpl of templates) {
    const tplId = String((tpl as Record<string, unknown>)._id ?? '');
    const tplName = String((tpl as Record<string, unknown>).name ?? '');
    if (!tplId) continue;

    const enrichedRows = await deps.viewDataService
      .getEnrichedRows(orgId, tplId)
      .catch(() => [] as Array<Record<string, unknown>>);

    for (const row of enrichedRows) {
      const cells = (row.cells as Record<string, unknown>) ?? {};
      const rowId = String(row.rowId ?? '');
      if (!matchesPart(cells, rowId, targetPart, options.partColumn)) {
        continue;
      }

      const rowCurrency = detectCurrencyFromRecord(cells);
      const directVendor = extractField(
        cells,
        options.vendorColumn,
        VENDOR_COLUMN_CANDIDATES,
      );
      const directPriceRaw = extractField(
        cells,
        options.priceColumn,
        PRICE_COLUMN_CANDIDATES,
      );

      if (directVendor !== undefined || directPriceRaw !== undefined) {
        const parsed = parsePriceAndCurrency(directPriceRaw, rowCurrency);
        if (parsed === null) {
          if (directPriceRaw !== undefined && String(directPriceRaw).trim() !== '') {
            ignoredInvalidCount += 1;
          }
        } else if (!requestedCurrency || parsed.currency === requestedCurrency) {
          offers.push({
            vendor: directVendor?.trim() || 'Unknown Vendor',
            price: parsed.price,
            rawPrice: String(directPriceRaw ?? ''),
            currency: parsed.currency,
            sourceTemplateId: tplId,
            sourceTemplateName: tplName,
            rowId,
          });
        }
      }

      const linkedData = (row.linkedData as Record<string, unknown>) ?? {};
      for (const linkedValue of Object.values(linkedData)) {
        if (!Array.isArray(linkedValue)) continue;
        for (const linkedRow of linkedValue) {
          if (!linkedRow || typeof linkedRow !== 'object') continue;
          const linkedCells = linkedRow as Record<string, unknown>;
          const linkedVendor = extractField(
            linkedCells,
            options.vendorColumn,
            VENDOR_COLUMN_CANDIDATES,
          );
          const linkedPriceRaw = extractField(
            linkedCells,
            options.priceColumn,
            PRICE_COLUMN_CANDIDATES,
          );
          if (linkedPriceRaw === undefined) continue;

          const linkedCurr =
            detectCurrencyFromRecord(linkedCells) ?? rowCurrency;
          const parsed = parsePriceAndCurrency(linkedPriceRaw, linkedCurr);
          if (parsed === null) {
            if (String(linkedPriceRaw).trim() !== '') {
              ignoredInvalidCount += 1;
            }
            continue;
          }
          if (requestedCurrency && parsed.currency !== requestedCurrency) {
            continue;
          }

          offers.push({
            vendor: linkedVendor?.trim() || directVendor?.trim() || 'Unknown Vendor',
            price: parsed.price,
            rawPrice: String(linkedPriceRaw),
            currency: parsed.currency,
            sourceTemplateId: tplId,
            sourceTemplateName: tplName,
            rowId,
          });
        }
      }
    }
  }

  // Fallback to central PartsService metadata if no template offers matched
  if (offers.length === 0) {
    const partsRes = await deps.partsService.list(
      orgId,
      options.templateId,
      options.partIdentifier.trim(),
      1,
      AI_TOOL_MAX_RECORDS,
    );
    const items = Array.isArray((partsRes as Record<string, unknown>).items)
      ? ((partsRes as Record<string, unknown>).items as Array<Record<string, unknown>>)
      : [];

    for (const part of items) {
      const metadata = (part.metadata as Record<string, unknown>) ?? {};
      const rowId = String(part.rowId ?? part._id ?? '');
      if (!matchesPart(metadata, rowId, targetPart, options.partColumn)) {
        continue;
      }

      const vendor = extractField(
        metadata,
        options.vendorColumn,
        VENDOR_COLUMN_CANDIDATES,
      );
      const priceRaw = extractField(
        metadata,
        options.priceColumn,
        PRICE_COLUMN_CANDIDATES,
      );
      const curr = detectCurrencyFromRecord(metadata);
      const parsed = parsePriceAndCurrency(priceRaw, curr);
      if (parsed === null) {
        if (priceRaw !== undefined && String(priceRaw).trim() !== '') {
          ignoredInvalidCount += 1;
        }
        continue;
      }
      if (requestedCurrency && parsed.currency !== requestedCurrency) {
        continue;
      }

      const tplId = String(part.templateId ?? '');
      offers.push({
        vendor: vendor?.trim() || 'Unknown Vendor',
        price: parsed.price,
        rawPrice: String(priceRaw ?? ''),
        currency: parsed.currency,
        sourceTemplateId: tplId,
        sourceTemplateName: templateNameById.get(tplId) ?? '',
        rowId,
      });
    }
  }

  const currenciesFound = Array.from(new Set(offers.map((o) => o.currency)));
  const comparable =
    offers.length > 0 &&
    currenciesFound.length === 1 &&
    currenciesFound[0] !== 'UNKNOWN';

  const sorted = [...offers].sort((a, b) => {
    if (a.price !== b.price) return a.price - b.price;
    return a.vendor.localeCompare(b.vendor);
  });

  const sourceTemplateMap = new Map<string, string>();
  for (const o of sorted) {
    if (o.sourceTemplateId) {
      sourceTemplateMap.set(o.sourceTemplateId, o.sourceTemplateName);
    }
  }

  return {
    partIdentifier: options.partIdentifier.trim(),
    offers: sorted,
    ignoredInvalidCount,
    comparable,
    currenciesFound,
    currency:
      currenciesFound.length === 1 ? currenciesFound[0] : currenciesFound.length > 1 ? 'MIXED' : 'UNKNOWN',
    lowestPrice: comparable ? sorted[0].price : null,
    highestPrice: comparable ? sorted[sorted.length - 1].price : null,
    cheapestVendor: comparable ? sorted[0] : null,
    sourceTemplates: Array.from(sourceTemplateMap.entries()).map(([id, name]) => ({
      id,
      name,
    })),
  };
}

function matchesPart(
  record: Record<string, unknown>,
  rowId: string,
  targetLower: string,
  explicitPartColumn?: string,
): boolean {
  if (explicitPartColumn) {
    const val = findCaseInsensitiveValue(record, explicitPartColumn);
    return val !== undefined && val.trim().toLowerCase() === targetLower;
  }

  if (rowId.trim().toLowerCase() === targetLower) {
    return true;
  }

  for (const [key, val] of Object.entries(record)) {
    const normalizedKey = key.trim().toLowerCase();
    if (
      PART_COLUMN_CANDIDATES.includes(normalizedKey) &&
      String(val ?? '').trim().toLowerCase() === targetLower
    ) {
      return true;
    }
  }
  return false;
}

function extractField(
  record: Record<string, unknown>,
  explicitColumn: string | undefined,
  candidates: string[],
): string | undefined {
  if (explicitColumn) {
    return findCaseInsensitiveValue(record, explicitColumn);
  }
  for (const [key, val] of Object.entries(record)) {
    if (candidates.includes(key.trim().toLowerCase())) {
      return val === undefined || val === null ? undefined : String(val);
    }
  }
  return undefined;
}

function findCaseInsensitiveValue(
  record: Record<string, unknown>,
  columnName: string,
): string | undefined {
  const target = columnName.trim().toLowerCase();
  for (const [key, val] of Object.entries(record)) {
    if (key.trim().toLowerCase() === target) {
      return val === undefined || val === null ? undefined : String(val);
    }
  }
  return undefined;
}

function detectCurrencyFromRecord(
  record: Record<string, unknown>,
): string | undefined {
  const raw = extractField(record, undefined, CURRENCY_COLUMN_CANDIDATES);
  const trimmed = raw?.trim().toUpperCase();
  return trimmed ? trimmed : undefined;
}

function parsePriceAndCurrency(
  rawPrice: string | undefined,
  explicitCurrency?: string,
): { price: number; currency: string } | null {
  if (rawPrice === undefined || rawPrice === null) {
    return null;
  }
  const text = String(rawPrice).trim();
  if (!text) {
    return null;
  }

  let detectedCurrency = explicitCurrency;
  if (!detectedCurrency) {
    for (const [pattern, code] of CURRENCY_SYMBOL_MAP) {
      if (pattern.test(text)) {
        detectedCurrency = code;
        break;
      }
    }
  }

  const cleaned = text
    .replace(/,/g, '')
    .replace(/[^\d.-]/g, '')
    .trim();
  if (!cleaned || !/^-?\d+(\.\d+)?$/.test(cleaned)) {
    return null;
  }

  const price = Number(cleaned);
  if (!Number.isFinite(price) || price < 0) {
    return null;
  }

  return {
    price,
    currency: detectedCurrency ?? 'UNKNOWN',
  };
}
