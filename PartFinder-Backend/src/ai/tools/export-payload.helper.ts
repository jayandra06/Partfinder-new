import type { AiExportFormat } from './dto/batch3-export-tool-args.dto';

export interface AiExportColumn {
  key: string;
  label: string;
}

export interface AiStructuredExportPayload {
  exportType:
    | 'template_data'
    | 'vendor_comparison'
    | 'missing_values_report'
    | 'duplicate_records_report';
  format: AiExportFormat;
  fileNameSuggestion: string;
  columns: AiExportColumn[];
  rows: Array<Record<string, string>>;
  csvContent?: string;
  rowCount: number;
  truncated: boolean;
  metadata?: Record<string, unknown>;
}

const FORMULA_TRIGGER_RE = /^[\s\u0000]*[=+\-@\t\r]/;

/**
 * Prevents spreadsheet formula injection (CSV/Excel DDE) by prefixing dangerous
 * leading characters with a single quote and applying RFC 4180 CSV quoting.
 */
export function sanitizeCsvCell(raw: unknown): string {
  const str = raw === undefined || raw === null ? '' : String(raw);
  const neutralized = FORMULA_TRIGGER_RE.test(str) ? `'${str}` : str;
  if (/[",\r\n]/.test(neutralized)) {
    return `"${neutralized.replace(/"/g, '""')}"`;
  }
  return neutralized;
}

export function buildFormulaSafeCsv(
  columns: AiExportColumn[],
  rows: Array<Record<string, string>>,
): string {
  const headerLine = columns.map((c) => sanitizeCsvCell(c.label)).join(',');
  const dataLines = rows.map((row) =>
    columns.map((c) => sanitizeCsvCell(row[c.key] ?? '')).join(','),
  );
  return [headerLine, ...dataLines].join('\r\n');
}

export function sanitizeFileName(base: string, format: AiExportFormat): string {
  const slug = base
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9_-]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 64);
  const ext = format === 'csv' ? 'csv' : 'xlsx';
  return `${slug || 'partfinder-export'}.${ext}`;
}
