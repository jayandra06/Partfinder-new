export const AI_TOOL_MAX_RECORDS = 50;
export const AI_TOOL_MAX_EXPORT_ROWS = 500;

export type Batch1AiToolName =
  | 'get_template_schema'
  | 'search_template_rows'
  | 'get_template_relationships'
  | 'get_part_details';

export type Batch2AiToolName =
  | 'compare_vendor_prices'
  | 'find_cheapest_vendor'
  | 'find_missing_values'
  | 'find_duplicate_records';

export type Batch3WmsAiToolName =
  | 'get_part_inventory'
  | 'find_part_warehouse'
  | 'get_warehouse_details'
  | 'check_stock_availability';

export type Batch3ExportAiToolName =
  | 'export_template_data'
  | 'export_vendor_comparison'
  | 'export_missing_values_report'
  | 'export_duplicate_records_report';

export type AiToolName =
  | Batch1AiToolName
  | Batch2AiToolName
  | Batch3WmsAiToolName
  | Batch3ExportAiToolName;

export interface AiToolExecutionContext {
  orgId: string;
  userId: string;
  email: string;
  role: string;
  requestId?: string;
}

export interface AiToolPermissionRequirement {
  obj: string;
  act: string;
}

export interface AiToolDefinition {
  name: AiToolName;
  description: string;
  requiredPermission: AiToolPermissionRequirement;
  readOnly: true;
  requiresConfirmation: false;
  maxResultLimit: number;
  parametersSchema: Record<string, unknown>;
}

export interface AiToolExecutionResult<T = unknown> {
  ok: boolean;
  tool: string;
  dataSource: 'central_mongodb' | 'tenant_mongodb_wms';
  data?: T;
  truncated?: boolean;
  returnedCount?: number;
  tenantDatabaseNote?: string;
  error?: {
    code:
      | 'UNKNOWN_TOOL'
      | 'INVALID_ARGUMENTS'
      | 'FORBIDDEN'
      | 'TENANT_REQUIRED'
      | 'NOT_FOUND'
      | 'EXECUTION_FAILED';
    message: string;
  };
}
