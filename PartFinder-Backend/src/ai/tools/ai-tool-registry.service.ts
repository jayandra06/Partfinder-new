import {
  BadRequestException,
  ForbiddenException,
  Injectable,
  Logger,
  NotFoundException,
} from '@nestjs/common';
import { ClassConstructor, plainToInstance } from 'class-transformer';
import { validate, ValidationError } from 'class-validator';
import { PartsService } from '../../parts/parts.service';
import { RelationsService } from '../../relations/relations.service';
import { TemplatesService } from '../../templates/templates.service';
import { ViewDataService } from '../../view-data/view-data.service';
import { WmsContextResolverService } from '../../wms/services/wms-context-resolver.service';
import { WmsInventoryReadService } from '../../wms/services/wms-inventory-read.service';
import { WmsSetupService } from '../../wms/services/wms-setup.service';
import { WmsSkuService } from '../../wms/services/wms-sku.service';
import type { WmsRequestContext } from '../../wms/wms.types';
import { AiToolAuthService } from './ai-tool-auth.service';
import {
  AI_TOOL_MAX_EXPORT_ROWS,
  AI_TOOL_MAX_RECORDS,
  AiToolDefinition,
  AiToolExecutionContext,
  AiToolExecutionResult,
  AiToolName,
} from './ai-tool.types';
import {
  GetPartDetailsArgsDto,
  GetTemplateRelationshipsArgsDto,
  GetTemplateSchemaArgsDto,
  SearchTemplateRowsArgsDto,
} from './dto/batch1-tool-args.dto';
import {
  CompareVendorPricesArgsDto,
  FindCheapestVendorArgsDto,
  FindDuplicateRecordsArgsDto,
  FindMissingValuesArgsDto,
} from './dto/batch2-tool-args.dto';
import {
  CheckStockAvailabilityArgsDto,
  FindPartWarehouseArgsDto,
  GetPartInventoryArgsDto,
  GetWarehouseDetailsArgsDto,
} from './dto/batch3-wms-tool-args.dto';
import {
  ExportDuplicateRecordsReportArgsDto,
  ExportMissingValuesReportArgsDto,
  ExportTemplateDataArgsDto,
  ExportVendorComparisonArgsDto,
} from './dto/batch3-export-tool-args.dto';
import {
  AiExportColumn,
  AiStructuredExportPayload,
  buildFormulaSafeCsv,
  sanitizeFileName,
} from './export-payload.helper';
import { extractVendorPriceOffers } from './vendor-price.helper';

interface RegisteredTool<TArgs extends object> {
  definition: AiToolDefinition;
  argsDto: ClassConstructor<TArgs>;
  handler: (
    orgId: string,
    args: TArgs,
    tenantDatabaseConfigured: boolean,
    context: AiToolExecutionContext,
  ) => Promise<AiToolExecutionResult>;
}

const TENANT_TEMPLATES_NOTE =
  'No matching records were found in central MongoDB (templates/template_rows). Note: desktop WinUI Master Data & Analytics templates may be stored in tenant MongoDB (orgDatabaseUri: partfinder_templates / partfinder_master_data_rows), which is not directly queried by central TemplatesService.';

const TENANT_PARTS_NOTE =
  'No matching records were found in central MongoDB (parts). Note: desktop WinUI parts & Master Data rows may be stored in tenant MongoDB (orgDatabaseUri: partfinder_master_data_rows), which is not directly queried by central PartsService.';

@Injectable()
export class AiToolRegistryService {
  private readonly logger = new Logger(AiToolRegistryService.name);
  private readonly registry: Record<AiToolName, RegisteredTool<any>>;

  constructor(
    private readonly authService: AiToolAuthService,
    private readonly templatesService: TemplatesService,
    private readonly viewDataService: ViewDataService,
    private readonly relationsService: RelationsService,
    private readonly partsService: PartsService,
    private readonly wmsContextResolver: WmsContextResolverService,
    private readonly wmsSetupService: WmsSetupService,
    private readonly wmsInventoryReadService: WmsInventoryReadService,
    private readonly wmsSkuService: WmsSkuService,
  ) {
    this.registry = {
      get_template_schema: {
        definition: {
          name: 'get_template_schema',
          description:
            'List central templates for the organization or fetch the column schema for a specific template.',
          requiredPermission: { obj: 'templates', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            properties: {
              templateId: {
                type: 'string',
                description:
                  'Optional central template ObjectId. Omit to list templates.',
              },
              includeColumns: {
                type: 'boolean',
                description: 'Whether to include column definitions.',
              },
            },
          },
        },
        argsDto: GetTemplateSchemaArgsDto,
        handler: (orgId, args, tenantDbConfigured) =>
          this.runGetTemplateSchema(orgId, args, tenantDbConfigured),
      },
      search_template_rows: {
        definition: {
          name: 'search_template_rows',
          description:
            'Search or paginate rows in a central template, optionally including linked relation columns.',
          requiredPermission: { obj: 'templates', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            required: ['templateId'],
            properties: {
              templateId: { type: 'string' },
              query: { type: 'string' },
              columnFilter: { type: 'string' },
              includeLinkedData: { type: 'boolean' },
              page: { type: 'integer', minimum: 1, maximum: 1000 },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_RECORDS,
              },
            },
          },
        },
        argsDto: SearchTemplateRowsArgsDto,
        handler: (orgId, args, tenantDbConfigured) =>
          this.runSearchTemplateRows(orgId, args, tenantDbConfigured),
      },
      get_template_relationships: {
        definition: {
          name: 'get_template_relationships',
          description:
            'Retrieve configured worksheet relationships, match keys, and display columns for the organization.',
          requiredPermission: { obj: 'templates', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            properties: {
              templateId: { type: 'string' },
              relationId: { type: 'string' },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_RECORDS,
              },
            },
          },
        },
        argsDto: GetTemplateRelationshipsArgsDto,
        handler: (orgId, args, tenantDbConfigured) =>
          this.runGetTemplateRelationships(orgId, args, tenantDbConfigured),
      },
      get_part_details: {
        definition: {
          name: 'get_part_details',
          description:
            'Fetch a specific central part by ID or search/list central parts for the organization.',
          requiredPermission: { obj: 'parts', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            properties: {
              partId: { type: 'string' },
              templateId: { type: 'string' },
              search: { type: 'string' },
              page: { type: 'integer', minimum: 1, maximum: 1000 },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_RECORDS,
              },
            },
          },
        },
        argsDto: GetPartDetailsArgsDto,
        handler: (orgId, args, tenantDbConfigured) =>
          this.runGetPartDetails(orgId, args, tenantDbConfigured),
      },
      compare_vendor_prices: {
        definition: {
          name: 'compare_vendor_prices',
          description:
            'Compare vendor prices for a specific part across central templates, linked lookup tables, and parts metadata without comparing incompatible currencies.',
          requiredPermission: { obj: 'templates', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            required: ['partIdentifier'],
            properties: {
              partIdentifier: { type: 'string', maxLength: 200 },
              templateId: { type: 'string' },
              partColumn: { type: 'string' },
              vendorColumn: { type: 'string' },
              priceColumn: { type: 'string' },
              currency: { type: 'string' },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_RECORDS,
              },
            },
          },
        },
        argsDto: CompareVendorPricesArgsDto,
        handler: (orgId, args, tenantDbConfigured) =>
          this.runCompareVendorPrices(orgId, args, tenantDbConfigured),
      },
      find_cheapest_vendor: {
        definition: {
          name: 'find_cheapest_vendor',
          description:
            'Identify the lowest-priced vendor and alternative offers for a specific part when prices share a compatible currency.',
          requiredPermission: { obj: 'templates', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            required: ['partIdentifier'],
            properties: {
              partIdentifier: { type: 'string', maxLength: 200 },
              templateId: { type: 'string' },
              partColumn: { type: 'string' },
              vendorColumn: { type: 'string' },
              priceColumn: { type: 'string' },
              currency: { type: 'string' },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_RECORDS,
              },
            },
          },
        },
        argsDto: FindCheapestVendorArgsDto,
        handler: (orgId, args, tenantDbConfigured) =>
          this.runFindCheapestVendor(orgId, args, tenantDbConfigured),
      },
      find_missing_values: {
        definition: {
          name: 'find_missing_values',
          description:
            'Find rows with missing, null, empty, or whitespace-only values in specified columns of a central template.',
          requiredPermission: { obj: 'templates', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            required: ['templateId'],
            properties: {
              templateId: { type: 'string' },
              columnIds: { type: 'array', items: { type: 'string' } },
              columnNames: { type: 'array', items: { type: 'string' } },
              missingCriteria: {
                type: 'string',
                enum: ['blank', 'null_or_empty', 'include_na'],
              },
              page: { type: 'integer', minimum: 1, maximum: 1000 },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_RECORDS,
              },
            },
          },
        },
        argsDto: FindMissingValuesArgsDto,
        handler: (orgId, args, tenantDbConfigured) =>
          this.runFindMissingValues(orgId, args, tenantDbConfigured),
      },
      find_duplicate_records: {
        definition: {
          name: 'find_duplicate_records',
          description:
            'Detect duplicate records in a central template grouped by one or more key columns.',
          requiredPermission: { obj: 'templates', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            required: ['templateId'],
            properties: {
              templateId: { type: 'string' },
              columnIds: { type: 'array', items: { type: 'string' } },
              columnNames: { type: 'array', items: { type: 'string' } },
              caseSensitive: { type: 'boolean' },
              ignoreEmpty: { type: 'boolean' },
              page: { type: 'integer', minimum: 1, maximum: 1000 },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_RECORDS,
              },
            },
          },
        },
        argsDto: FindDuplicateRecordsArgsDto,
        handler: (orgId, args, tenantDbConfigured) =>
          this.runFindDuplicateRecords(orgId, args, tenantDbConfigured),
      },
      get_part_inventory: {
        definition: {
          name: 'get_part_inventory',
          description:
            'Retrieve WMS stock balances (on-hand, reserved, blocked, available ATP) for a SKU or part identifier across permitted warehouses.',
          requiredPermission: { obj: 'wms', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            properties: {
              skuId: { type: 'string' },
              partIdentifier: { type: 'string', maxLength: 200 },
              warehouseId: { type: 'string' },
              ownerId: { type: 'string' },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_RECORDS,
              },
            },
          },
        },
        argsDto: GetPartInventoryArgsDto,
        handler: (orgId, args, _tenantDbConfigured, context) =>
          this.runGetPartInventory(orgId, args, context),
      },
      find_part_warehouse: {
        definition: {
          name: 'find_part_warehouse',
          description:
            'Find which permitted WMS warehouses and locations currently hold stock for a SKU or part identifier.',
          requiredPermission: { obj: 'wms', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            properties: {
              skuId: { type: 'string' },
              partIdentifier: { type: 'string', maxLength: 200 },
              onlyAvailable: { type: 'boolean' },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_RECORDS,
              },
            },
          },
        },
        argsDto: FindPartWarehouseArgsDto,
        handler: (orgId, args, _tenantDbConfigured, context) =>
          this.runFindPartWarehouse(orgId, args, context),
      },
      get_warehouse_details: {
        definition: {
          name: 'get_warehouse_details',
          description:
            'List permitted WMS warehouses or retrieve details (code, name, address, timeZone, status) for a specific warehouse.',
          requiredPermission: { obj: 'wms', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            properties: {
              warehouseId: { type: 'string' },
              activeOnly: { type: 'boolean' },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_RECORDS,
              },
            },
          },
        },
        argsDto: GetWarehouseDetailsArgsDto,
        handler: (orgId, args, _tenantDbConfigured, context) =>
          this.runGetWarehouseDetails(orgId, args, context),
      },
      check_stock_availability: {
        definition: {
          name: 'check_stock_availability',
          description:
            'Check available-to-promise (ATP) stock in WMS for a SKU or part identifier, optionally comparing against a required quantity.',
          requiredPermission: { obj: 'wms', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            properties: {
              skuId: { type: 'string' },
              partIdentifier: { type: 'string', maxLength: 200 },
              warehouseId: { type: 'string' },
              ownerId: { type: 'string' },
              requiredQuantity: { type: 'number', minimum: 0 },
            },
          },
        },
        argsDto: CheckStockAvailabilityArgsDto,
        handler: (orgId, args, _tenantDbConfigured, context) =>
          this.runCheckStockAvailability(orgId, args, context),
      },
      export_template_data: {
        definition: {
          name: 'export_template_data',
          description:
            'Build a bounded structured table or formula-safe CSV export payload for a central template.',
          requiredPermission: { obj: 'templates', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_EXPORT_ROWS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            required: ['templateId'],
            properties: {
              templateId: { type: 'string' },
              query: { type: 'string' },
              columnFilter: { type: 'string' },
              includeLinkedData: { type: 'boolean' },
              format: { type: 'string', enum: ['json_table', 'csv'] },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_EXPORT_ROWS,
              },
            },
          },
        },
        argsDto: ExportTemplateDataArgsDto,
        handler: (orgId, args, tenantDbConfigured) =>
          this.runExportTemplateData(orgId, args, tenantDbConfigured),
      },
      export_vendor_comparison: {
        definition: {
          name: 'export_vendor_comparison',
          description:
            'Build a structured table or formula-safe CSV export payload comparing vendor prices for a part.',
          requiredPermission: { obj: 'templates', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_RECORDS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            required: ['partIdentifier'],
            properties: {
              partIdentifier: { type: 'string', maxLength: 200 },
              templateId: { type: 'string' },
              partColumn: { type: 'string' },
              vendorColumn: { type: 'string' },
              priceColumn: { type: 'string' },
              currency: { type: 'string' },
              format: { type: 'string', enum: ['json_table', 'csv'] },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_RECORDS,
              },
            },
          },
        },
        argsDto: ExportVendorComparisonArgsDto,
        handler: (orgId, args, tenantDbConfigured) =>
          this.runExportVendorComparison(orgId, args, tenantDbConfigured),
      },
      export_missing_values_report: {
        definition: {
          name: 'export_missing_values_report',
          description:
            'Build a structured table or formula-safe CSV export payload of rows with missing values in a central template.',
          requiredPermission: { obj: 'templates', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_EXPORT_ROWS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            required: ['templateId'],
            properties: {
              templateId: { type: 'string' },
              columnIds: { type: 'array', items: { type: 'string' } },
              columnNames: { type: 'array', items: { type: 'string' } },
              missingCriteria: {
                type: 'string',
                enum: ['blank', 'null_or_empty', 'include_na'],
              },
              format: { type: 'string', enum: ['json_table', 'csv'] },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_EXPORT_ROWS,
              },
            },
          },
        },
        argsDto: ExportMissingValuesReportArgsDto,
        handler: (orgId, args, tenantDbConfigured) =>
          this.runExportMissingValuesReport(orgId, args, tenantDbConfigured),
      },
      export_duplicate_records_report: {
        definition: {
          name: 'export_duplicate_records_report',
          description:
            'Build a structured table or formula-safe CSV export payload of duplicate record groups in a central template.',
          requiredPermission: { obj: 'templates', act: 'view' },
          readOnly: true,
          requiresConfirmation: false,
          maxResultLimit: AI_TOOL_MAX_EXPORT_ROWS,
          parametersSchema: {
            type: 'object',
            additionalProperties: false,
            required: ['templateId'],
            properties: {
              templateId: { type: 'string' },
              columnIds: { type: 'array', items: { type: 'string' } },
              columnNames: { type: 'array', items: { type: 'string' } },
              caseSensitive: { type: 'boolean' },
              ignoreEmpty: { type: 'boolean' },
              format: { type: 'string', enum: ['json_table', 'csv'] },
              limit: {
                type: 'integer',
                minimum: 1,
                maximum: AI_TOOL_MAX_EXPORT_ROWS,
              },
            },
          },
        },
        argsDto: ExportDuplicateRecordsReportArgsDto,
        handler: (orgId, args, tenantDbConfigured) =>
          this.runExportDuplicateRecordsReport(orgId, args, tenantDbConfigured),
      },
    };
  }

  getToolDefinitions(): AiToolDefinition[] {
    return Object.values(this.registry).map((entry) => entry.definition);
  }

  isAllowedTool(name: string): name is AiToolName {
    return Object.prototype.hasOwnProperty.call(this.registry, name);
  }

  async executeTool(
    toolName: string,
    rawArgs: unknown,
    context: AiToolExecutionContext,
  ): Promise<AiToolExecutionResult> {
    const startedAt = Date.now();

    if (!this.isAllowedTool(toolName)) {
      return {
        ok: false,
        tool: toolName,
        dataSource: 'central_mongodb',
        error: {
          code: 'UNKNOWN_TOOL',
          message: `Tool "${toolName}" is not registered in the allowlist.`,
        },
      };
    }

    const orgId = context.orgId?.trim();
    if (!orgId) {
      return {
        ok: false,
        tool: toolName,
        dataSource: 'central_mongodb',
        error: {
          code: 'TENANT_REQUIRED',
          message: 'Organization context (orgId) is required.',
        },
      };
    }

    const entry = this.registry[toolName];

    const normalizedArgs = rawArgs === undefined || rawArgs === null ? {} : rawArgs;
    if (typeof normalizedArgs !== 'object' || Array.isArray(normalizedArgs)) {
      return {
        ok: false,
        tool: toolName,
        dataSource: 'central_mongodb',
        error: {
          code: 'INVALID_ARGUMENTS',
          message: 'Tool arguments must be a JSON object.',
        },
      };
    }

    const dtoInstance = plainToInstance(entry.argsDto, normalizedArgs);
    const validationErrors = await validate(dtoInstance, {
      whitelist: true,
      forbidNonWhitelisted: true,
    });
    if (validationErrors.length > 0) {
      return {
        ok: false,
        tool: toolName,
        dataSource: 'central_mongodb',
        error: {
          code: 'INVALID_ARGUMENTS',
          message: this.formatValidationErrors(validationErrors),
        },
      };
    }

    const auth = await this.authService.authorize(
      context,
      entry.definition.requiredPermission,
    );
    if (!auth.allowed) {
      this.logger.warn(
        `AI tool denied: tool=${toolName} org=${orgId} user=${context.userId} req=${context.requestId ?? 'none'}`,
      );
      return {
        ok: false,
        tool: toolName,
        dataSource: 'central_mongodb',
        error: {
          code: 'FORBIDDEN',
          message: auth.reason ?? 'Permission denied.',
        },
      };
    }

    try {
      const result = await entry.handler(
        orgId,
        dtoInstance,
        auth.tenantDatabaseConfigured,
        context,
      );
      this.logger.log(
        `AI tool executed: tool=${toolName} org=${orgId} user=${context.userId} ok=${result.ok} count=${result.returnedCount ?? 0} ms=${Date.now() - startedAt}`,
      );
      return result;
    } catch (error) {
      if (error instanceof ForbiddenException) {
        return {
          ok: false,
          tool: toolName,
          dataSource: 'central_mongodb',
          error: {
            code: 'FORBIDDEN',
            message: error.message,
          },
        };
      }
      if (error instanceof NotFoundException) {
        return {
          ok: false,
          tool: toolName,
          dataSource: 'central_mongodb',
          tenantDatabaseNote: auth.tenantDatabaseConfigured
            ? TENANT_TEMPLATES_NOTE
            : undefined,
          error: {
            code: 'NOT_FOUND',
            message: error.message,
          },
        };
      }
      if (error instanceof BadRequestException) {
        return {
          ok: false,
          tool: toolName,
          dataSource: 'central_mongodb',
          error: {
            code: 'INVALID_ARGUMENTS',
            message: error.message,
          },
        };
      }

      const message =
        error instanceof Error ? error.message : 'Tool execution failed.';
      this.logger.error(
        `AI tool failed: tool=${toolName} org=${orgId} user=${context.userId} err=${message}`,
      );
      return {
        ok: false,
        tool: toolName,
        dataSource: 'central_mongodb',
        error: {
          code: 'EXECUTION_FAILED',
          message,
        },
      };
    }
  }

  private async runGetTemplateSchema(
    orgId: string,
    args: GetTemplateSchemaArgsDto,
    tenantDatabaseConfigured: boolean,
  ): Promise<AiToolExecutionResult> {
    if (args.templateId) {
      const tpl = await this.templatesService.getOne(orgId, args.templateId);
      const rawColumns = Array.isArray(tpl.columns) ? tpl.columns : [];
      const columns =
        args.includeColumns === false
          ? undefined
          : rawColumns.slice(0, AI_TOOL_MAX_RECORDS).map((col) => {
              const c = col as Record<string, unknown>;
              return {
                id: String(c._id ?? ''),
                name: String(c.name ?? ''),
                order: Number(c.order ?? 0),
                type: String(c.type ?? 'text'),
                isLink: Boolean(c.isLink),
              };
            });

      return {
        ok: true,
        tool: 'get_template_schema',
        dataSource: 'central_mongodb',
        returnedCount: 1,
        truncated: rawColumns.length > AI_TOOL_MAX_RECORDS,
        data: {
          template: {
            id: String(tpl._id ?? args.templateId),
            name: String(tpl.name ?? ''),
            columnCount: rawColumns.length,
            ...(columns !== undefined ? { columns } : {}),
          },
        },
      };
    }

    const list = await this.templatesService.list(orgId);
    const bounded = list.slice(0, AI_TOOL_MAX_RECORDS).map((t) => ({
      id: String((t as Record<string, unknown>)._id ?? ''),
      name: String((t as Record<string, unknown>).name ?? ''),
    }));

    return {
      ok: true,
      tool: 'get_template_schema',
      dataSource: 'central_mongodb',
      returnedCount: bounded.length,
      truncated: list.length > AI_TOOL_MAX_RECORDS,
      tenantDatabaseNote:
        bounded.length === 0 && tenantDatabaseConfigured
          ? TENANT_TEMPLATES_NOTE
          : undefined,
      data: {
        totalTemplates: list.length,
        templates: bounded,
      },
    };
  }

  private async runSearchTemplateRows(
    orgId: string,
    args: SearchTemplateRowsArgsDto,
    tenantDatabaseConfigured: boolean,
  ): Promise<AiToolExecutionResult> {
    const limit = Math.min(
      args.limit ?? AI_TOOL_MAX_RECORDS,
      AI_TOOL_MAX_RECORDS,
    );
    const page = args.page ?? 1;
    const queryLower = args.query?.trim().toLowerCase() ?? '';
    const columnFilter = args.columnFilter?.trim();

    if (args.includeLinkedData) {
      const enriched = await this.viewDataService.getEnrichedRows(
        orgId,
        args.templateId,
      );
      const filtered = enriched.filter((row) =>
        this.matchesRowQuery(
          row.cells as Record<string, unknown> | undefined,
          String(row.rowId ?? ''),
          queryLower,
          columnFilter,
        ),
      );
      const start = (page - 1) * limit;
      const paged = filtered.slice(start, start + limit);

      return {
        ok: true,
        tool: 'search_template_rows',
        dataSource: 'central_mongodb',
        returnedCount: paged.length,
        truncated: filtered.length > start + paged.length,
        tenantDatabaseNote:
          filtered.length === 0 && tenantDatabaseConfigured
            ? TENANT_TEMPLATES_NOTE
            : undefined,
        data: {
          templateId: args.templateId,
          page,
          limit,
          totalMatched: filtered.length,
          rows: paged,
        },
      };
    }

    const res = await this.templatesService.getRows(
      orgId,
      args.templateId,
      page,
      limit,
    );
    const rawRows = Array.isArray(res.rows)
      ? (res.rows as Array<Record<string, unknown>>)
      : [];
    const filtered = rawRows
      .filter((row) =>
        this.matchesRowQuery(
          row.cells as Record<string, unknown> | undefined,
          String(row._id ?? ''),
          queryLower,
          columnFilter,
        ),
      )
      .slice(0, limit);

    return {
      ok: true,
      tool: 'search_template_rows',
      dataSource: 'central_mongodb',
      returnedCount: filtered.length,
      truncated:
        Number(res.total ?? filtered.length) > page * limit ||
        rawRows.length > limit,
      tenantDatabaseNote:
        filtered.length === 0 && tenantDatabaseConfigured
          ? TENANT_TEMPLATES_NOTE
          : undefined,
      data: {
        templateId: args.templateId,
        page,
        limit,
        totalInTemplate: Number(res.total ?? filtered.length),
        rows: filtered.map((r) => ({
          rowId: String(r._id ?? ''),
          rowIndex: Number(r.rowIndex ?? 0),
          cells: (r.cells as Record<string, unknown>) ?? {},
        })),
      },
    };
  }

  private async runGetTemplateRelationships(
    orgId: string,
    args: GetTemplateRelationshipsArgsDto,
    tenantDatabaseConfigured: boolean,
  ): Promise<AiToolExecutionResult> {
    const limit = Math.min(
      args.limit ?? AI_TOOL_MAX_RECORDS,
      AI_TOOL_MAX_RECORDS,
    );

    if (args.relationId) {
      const relation = await this.relationsService.getOne(
        orgId,
        args.relationId,
      );
      return {
        ok: true,
        tool: 'get_template_relationships',
        dataSource: 'central_mongodb',
        returnedCount: 1,
        truncated: false,
        data: {
          relationships: [this.sanitizeRelation(relation)],
        },
      };
    }

    const all = await this.relationsService.list(orgId);
    const filtered = args.templateId
      ? all.filter((r) => {
          const rec = r as Record<string, unknown>;
          return (
            String(rec.primaryTemplateId ?? '') === args.templateId ||
            String(rec.lookupTemplateId ?? '') === args.templateId
          );
        })
      : all;

    const bounded = filtered
      .slice(0, limit)
      .map((r) => this.sanitizeRelation(r as Record<string, unknown>));

    return {
      ok: true,
      tool: 'get_template_relationships',
      dataSource: 'central_mongodb',
      returnedCount: bounded.length,
      truncated: filtered.length > limit,
      tenantDatabaseNote:
        bounded.length === 0 && tenantDatabaseConfigured
          ? TENANT_TEMPLATES_NOTE
          : undefined,
      data: {
        totalRelationships: filtered.length,
        relationships: bounded,
      },
    };
  }

  private async runGetPartDetails(
    orgId: string,
    args: GetPartDetailsArgsDto,
    tenantDatabaseConfigured: boolean,
  ): Promise<AiToolExecutionResult> {
    const limit = Math.min(
      args.limit ?? AI_TOOL_MAX_RECORDS,
      AI_TOOL_MAX_RECORDS,
    );

    if (args.partId) {
      try {
        const part = await this.partsService.getById(orgId, args.partId);
        return {
          ok: true,
          tool: 'get_part_details',
          dataSource: 'central_mongodb',
          returnedCount: 1,
          truncated: false,
          data: {
            part: this.sanitizePart(part as Record<string, unknown>),
          },
        };
      } catch (err) {
        if (err instanceof NotFoundException) {
          return {
            ok: false,
            tool: 'get_part_details',
            dataSource: 'central_mongodb',
            tenantDatabaseNote: tenantDatabaseConfigured
              ? TENANT_PARTS_NOTE
              : undefined,
            error: {
              code: 'NOT_FOUND',
              message: err.message,
            },
          };
        }
        throw err;
      }
    }

    const page = args.page ?? 1;
    const res = await this.partsService.list(
      orgId,
      args.templateId,
      args.search,
      page,
      limit,
    );
    const items = Array.isArray((res as Record<string, unknown>).items)
      ? ((res as Record<string, unknown>).items as Array<Record<string, unknown>>)
      : [];
    const bounded = items
      .slice(0, limit)
      .map((item) => this.sanitizePart(item));
    const total = Number((res as Record<string, unknown>).total ?? bounded.length);

    return {
      ok: true,
      tool: 'get_part_details',
      dataSource: 'central_mongodb',
      returnedCount: bounded.length,
      truncated: total > page * limit || items.length > limit,
      tenantDatabaseNote:
        bounded.length === 0 && tenantDatabaseConfigured
          ? TENANT_PARTS_NOTE
          : undefined,
      data: {
        page,
        limit,
        totalParts: total,
        parts: bounded,
      },
    };
  }

  private matchesRowQuery(
    cells: Record<string, unknown> | undefined,
    rowId: string,
    queryLower: string,
    columnFilter?: string,
  ): boolean {
    if (!queryLower) {
      if (!columnFilter) {
        return true;
      }
      return Boolean(cells && columnFilter in cells);
    }

    if (!cells) {
      return rowId.toLowerCase().includes(queryLower);
    }

    if (columnFilter) {
      const val = cells[columnFilter];
      return val !== undefined && String(val).toLowerCase().includes(queryLower);
    }

    if (rowId.toLowerCase().includes(queryLower)) {
      return true;
    }

    return Object.values(cells).some((v) =>
      String(v ?? '').toLowerCase().includes(queryLower),
    );
  }

  private sanitizeRelation(relation: Record<string, unknown>) {
    return {
      id: String(relation._id ?? ''),
      name: String(relation.name ?? ''),
      primaryTemplateId: String(relation.primaryTemplateId ?? ''),
      lookupTemplateId: String(relation.lookupTemplateId ?? ''),
      triggerColumn: String(relation.triggerColumn ?? ''),
      menuLabel: String(relation.menuLabel ?? ''),
      matchKeys: Array.isArray(relation.matchKeys) ? relation.matchKeys : [],
      displayColumns: Array.isArray(relation.displayColumns)
        ? relation.displayColumns
        : [],
    };
  }

  private async runCompareVendorPrices(
    orgId: string,
    args: CompareVendorPricesArgsDto,
    tenantDatabaseConfigured: boolean,
  ): Promise<AiToolExecutionResult> {
    const limit = Math.min(
      args.limit ?? AI_TOOL_MAX_RECORDS,
      AI_TOOL_MAX_RECORDS,
    );
    const summary = await extractVendorPriceOffers(
      {
        templatesService: this.templatesService,
        viewDataService: this.viewDataService,
        partsService: this.partsService,
      },
      orgId,
      {
        partIdentifier: args.partIdentifier,
        templateId: args.templateId,
        partColumn: args.partColumn,
        vendorColumn: args.vendorColumn,
        priceColumn: args.priceColumn,
        currency: args.currency,
      },
    );

    const boundedOffers = summary.offers.slice(0, limit);

    return {
      ok: true,
      tool: 'compare_vendor_prices',
      dataSource: 'central_mongodb',
      returnedCount: boundedOffers.length,
      truncated: summary.offers.length > limit,
      tenantDatabaseNote:
        summary.offers.length === 0 && tenantDatabaseConfigured
          ? TENANT_TEMPLATES_NOTE
          : undefined,
      data: {
        partIdentifier: summary.partIdentifier,
        comparable: summary.comparable,
        currency: summary.currency,
        currenciesFound: summary.currenciesFound,
        lowestPrice: summary.lowestPrice,
        highestPrice: summary.highestPrice,
        vendorsCompared: summary.offers.length,
        ignoredInvalidCount: summary.ignoredInvalidCount,
        sourceTemplates: summary.sourceTemplates,
        offers: boundedOffers,
      },
    };
  }

  private async runFindCheapestVendor(
    orgId: string,
    args: FindCheapestVendorArgsDto,
    tenantDatabaseConfigured: boolean,
  ): Promise<AiToolExecutionResult> {
    const limit = Math.min(args.limit ?? 10, AI_TOOL_MAX_RECORDS);
    const summary = await extractVendorPriceOffers(
      {
        templatesService: this.templatesService,
        viewDataService: this.viewDataService,
        partsService: this.partsService,
      },
      orgId,
      {
        partIdentifier: args.partIdentifier,
        templateId: args.templateId,
        partColumn: args.partColumn,
        vendorColumn: args.vendorColumn,
        priceColumn: args.priceColumn,
        currency: args.currency,
      },
    );

    const maxAlternatives = summary.comparable
      ? Math.max(0, limit - (summary.cheapestVendor ? 1 : 0))
      : limit;

    const alternatives = summary.comparable
      ? summary.offers.slice(1, 1 + maxAlternatives)
      : summary.offers.slice(0, limit);

    const totalReturned = summary.comparable
      ? (summary.cheapestVendor ? 1 : 0) + alternatives.length
      : alternatives.length;

    return {
      ok: true,
      tool: 'find_cheapest_vendor',
      dataSource: 'central_mongodb',
      returnedCount: totalReturned,
      truncated: summary.offers.length > totalReturned,
      tenantDatabaseNote:
        summary.offers.length === 0 && tenantDatabaseConfigured
          ? TENANT_TEMPLATES_NOTE
          : undefined,
      data: {
        partIdentifier: summary.partIdentifier,
        comparable: summary.comparable,
        currency: summary.currency,
        currenciesFound: summary.currenciesFound,
        cheapestVendor: summary.cheapestVendor,
        lowestPrice: summary.lowestPrice,
        alternatives,
        vendorsCompared: summary.offers.length,
        ignoredInvalidCount: summary.ignoredInvalidCount,
        sourceTemplates: summary.sourceTemplates,
      },
    };
  }

  private async runFindMissingValues(
    orgId: string,
    args: FindMissingValuesArgsDto,
    tenantDatabaseConfigured: boolean,
  ): Promise<AiToolExecutionResult> {
    const result = await this.templatesService.findMissingValues(
      orgId,
      args.templateId,
      {
        columnIds: args.columnIds,
        columnNames: args.columnNames,
        missingCriteria: args.missingCriteria,
        page: args.page,
        limit: args.limit,
      },
    );

    return {
      ok: true,
      tool: 'find_missing_values',
      dataSource: 'central_mongodb',
      returnedCount: result.rows.length,
      truncated:
        result.totalAffectedRows > result.page * result.limit,
      tenantDatabaseNote:
        result.totalAffectedRows === 0 && tenantDatabaseConfigured
          ? TENANT_TEMPLATES_NOTE
          : undefined,
      data: result,
    };
  }

  private async runFindDuplicateRecords(
    orgId: string,
    args: FindDuplicateRecordsArgsDto,
    tenantDatabaseConfigured: boolean,
  ): Promise<AiToolExecutionResult> {
    const result = await this.templatesService.findDuplicateRecords(
      orgId,
      args.templateId,
      {
        columnIds: args.columnIds,
        columnNames: args.columnNames,
        caseSensitive: args.caseSensitive,
        ignoreEmpty: args.ignoreEmpty,
        page: args.page,
        limit: args.limit,
      },
    );

    return {
      ok: true,
      tool: 'find_duplicate_records',
      dataSource: 'central_mongodb',
      returnedCount: result.duplicateGroups.length,
      truncated:
        result.totalDuplicateGroups > result.page * result.limit,
      tenantDatabaseNote:
        result.totalDuplicateGroups === 0 && tenantDatabaseConfigured
          ? TENANT_TEMPLATES_NOTE
          : undefined,
      data: result,
    };
  }

  private async runGetPartInventory(
    orgId: string,
    args: GetPartInventoryArgsDto,
    context: AiToolExecutionContext,
  ): Promise<AiToolExecutionResult> {
    const limit = Math.min(
      args.limit ?? AI_TOOL_MAX_RECORDS,
      AI_TOOL_MAX_RECORDS,
    );
    const wmsCtx = await this.wmsContextResolver.resolveContext(
      orgId,
      context.email,
    );
    const skus = await this.resolveWmsSkus(
      wmsCtx,
      args.skuId,
      args.partIdentifier,
    );
    const warehousesRes = await this.wmsSetupService.listWarehouses(wmsCtx);
    const warehouses = Array.isArray(warehousesRes.items)
      ? (warehousesRes.items as Array<{
          id: string;
          code: string;
          name: string;
          address: string;
          timeZone: string;
          isActive: boolean;
        }>)
      : [];
    const warehouseMap = new Map(warehouses.map((w) => [w.id, w]));

    const allBalances: Array<Record<string, unknown>> = [];
    let totalMatched = 0;

    for (const sku of skus) {
      const res = await this.wmsInventoryReadService.listBalances(wmsCtx, {
        warehouseId: args.warehouseId,
        ownerId: args.ownerId,
        skuId: sku.skuId,
        limit,
      });
      totalMatched += res.total;
      for (const b of res.items) {
        if (allBalances.length >= limit) break;
        const wh = warehouseMap.get(b.warehouseId);
        allBalances.push({
          skuId: b.skuId,
          skuName: sku.displayName,
          primaryUom: b.primaryUom || sku.primaryUom,
          warehouseId: b.warehouseId,
          warehouseCode: wh?.code ?? '',
          warehouseName: wh?.name ?? '',
          locationId: b.locationId,
          ownerId: b.ownerId,
          onHand: b.onHand,
          reserved: b.reserved,
          blockedQty: b.blockedQty,
          available: b.available,
          updatedAt: b.updatedAt ?? null,
        });
      }
      if (allBalances.length >= limit) break;
    }

    const totals = allBalances.reduce<{
      onHand: number;
      reserved: number;
      blockedQty: number;
      available: number;
    }>(
      (acc, item) => ({
        onHand: acc.onHand + Number(item.onHand ?? 0),
        reserved: acc.reserved + Number(item.reserved ?? 0),
        blockedQty: acc.blockedQty + Number(item.blockedQty ?? 0),
        available: acc.available + Number(item.available ?? 0),
      }),
      { onHand: 0, reserved: 0, blockedQty: 0, available: 0 },
    );

    return {
      ok: true,
      tool: 'get_part_inventory',
      dataSource: 'tenant_mongodb_wms',
      returnedCount: allBalances.length,
      truncated: totalMatched > allBalances.length,
      data: {
        matchedSkus: skus,
        totals,
        balances: allBalances,
      },
    };
  }

  private async runFindPartWarehouse(
    orgId: string,
    args: FindPartWarehouseArgsDto,
    context: AiToolExecutionContext,
  ): Promise<AiToolExecutionResult> {
    const limit = Math.min(
      args.limit ?? AI_TOOL_MAX_RECORDS,
      AI_TOOL_MAX_RECORDS,
    );
    const wmsCtx = await this.wmsContextResolver.resolveContext(
      orgId,
      context.email,
    );
    const skus = await this.resolveWmsSkus(
      wmsCtx,
      args.skuId,
      args.partIdentifier,
    );
    const warehousesRes = await this.wmsSetupService.listWarehouses(wmsCtx);
    const warehouses = Array.isArray(warehousesRes.items)
      ? (warehousesRes.items as Array<{
          id: string;
          code: string;
          name: string;
          address: string;
          timeZone: string;
          isActive: boolean;
        }>)
      : [];
    const warehouseMap = new Map(warehouses.map((w) => [w.id, w]));

    const byWarehouse = new Map<
      string,
      {
        warehouseId: string;
        code: string;
        name: string;
        address: string;
        onHand: number;
        reserved: number;
        available: number;
        locationIds: Set<string>;
      }
    >();

    for (const sku of skus) {
      const res = await this.wmsInventoryReadService.listBalances(wmsCtx, {
        skuId: sku.skuId,
        limit: AI_TOOL_MAX_RECORDS,
      });
      for (const b of res.items) {
        if (args.onlyAvailable && b.available <= 0) continue;
        const wh = warehouseMap.get(b.warehouseId);
        const existing = byWarehouse.get(b.warehouseId) ?? {
          warehouseId: b.warehouseId,
          code: wh?.code ?? '',
          name: wh?.name ?? '',
          address: wh?.address ?? '',
          onHand: 0,
          reserved: 0,
          available: 0,
          locationIds: new Set<string>(),
        };
        existing.onHand += b.onHand;
        existing.reserved += b.reserved;
        existing.available += b.available;
        if (b.locationId) existing.locationIds.add(b.locationId);
        byWarehouse.set(b.warehouseId, existing);
      }
    }

    const allWarehouses = Array.from(byWarehouse.values());
    const bounded = allWarehouses.slice(0, limit).map((w) => ({
      warehouseId: w.warehouseId,
      code: w.code,
      name: w.name,
      address: w.address,
      onHand: w.onHand,
      reserved: w.reserved,
      available: w.available,
      locationIds: Array.from(w.locationIds).slice(0, 20),
    }));

    return {
      ok: true,
      tool: 'find_part_warehouse',
      dataSource: 'tenant_mongodb_wms',
      returnedCount: bounded.length,
      truncated: allWarehouses.length > limit,
      data: {
        matchedSkus: skus,
        warehouses: bounded,
      },
    };
  }

  private async runGetWarehouseDetails(
    orgId: string,
    args: GetWarehouseDetailsArgsDto,
    context: AiToolExecutionContext,
  ): Promise<AiToolExecutionResult> {
    const limit = Math.min(
      args.limit ?? AI_TOOL_MAX_RECORDS,
      AI_TOOL_MAX_RECORDS,
    );
    const wmsCtx = await this.wmsContextResolver.resolveContext(
      orgId,
      context.email,
    );
    const warehousesRes = await this.wmsSetupService.listWarehouses(
      wmsCtx,
      args.activeOnly,
    );
    const warehouses = Array.isArray(warehousesRes.items)
      ? (warehousesRes.items as Array<{
          id: string;
          code: string;
          name: string;
          address: string;
          timeZone: string;
          isActive: boolean;
        }>)
      : [];

    const filtered = args.warehouseId
      ? warehouses.filter(
          (w) =>
            w.id === args.warehouseId ||
            w.code.toLowerCase() === args.warehouseId!.trim().toLowerCase(),
        )
      : warehouses;

    if (args.warehouseId && filtered.length === 0) {
      throw new NotFoundException(
        `Warehouse "${args.warehouseId}" not found or not permitted.`,
      );
    }

    const bounded = filtered.slice(0, limit).map((w) => ({
      id: w.id,
      code: w.code,
      name: w.name,
      address: w.address,
      timeZone: w.timeZone,
      isActive: w.isActive,
    }));

    return {
      ok: true,
      tool: 'get_warehouse_details',
      dataSource: 'tenant_mongodb_wms',
      returnedCount: bounded.length,
      truncated: filtered.length > limit,
      data: {
        totalPermittedWarehouses: filtered.length,
        warehouses: bounded,
      },
    };
  }

  private async runCheckStockAvailability(
    orgId: string,
    args: CheckStockAvailabilityArgsDto,
    context: AiToolExecutionContext,
  ): Promise<AiToolExecutionResult> {
    const wmsCtx = await this.wmsContextResolver.resolveContext(
      orgId,
      context.email,
    );
    const skus = await this.resolveWmsSkus(
      wmsCtx,
      args.skuId,
      args.partIdentifier,
    );

    let onHand = 0;
    let reserved = 0;
    let blockedQty = 0;
    let available = 0;

    if (args.ownerId && args.warehouseId && skus.length === 1) {
      const atp = await this.wmsInventoryReadService.computeAtpForSku(
        wmsCtx,
        args.ownerId,
        args.warehouseId,
        skus[0].skuId,
      );
      onHand = atp.onHand;
      reserved = atp.reserved;
      blockedQty = atp.blockedQty;
      available = atp.available;
    } else {
      for (const sku of skus) {
        const res = await this.wmsInventoryReadService.listBalances(wmsCtx, {
          warehouseId: args.warehouseId,
          ownerId: args.ownerId,
          skuId: sku.skuId,
          limit: AI_TOOL_MAX_RECORDS,
        });
        for (const b of res.items) {
          onHand += b.onHand;
          reserved += b.reserved;
          blockedQty += b.blockedQty;
          available += b.available;
        }
      }
    }

    const sufficientForRequired =
      args.requiredQuantity !== undefined
        ? available >= args.requiredQuantity
        : undefined;

    return {
      ok: true,
      tool: 'check_stock_availability',
      dataSource: 'tenant_mongodb_wms',
      returnedCount: skus.length,
      truncated: false,
      data: {
        matchedSkus: skus,
        warehouseId: args.warehouseId ?? null,
        ownerId: args.ownerId ?? null,
        onHand,
        reserved,
        blockedQty,
        available,
        ...(args.requiredQuantity !== undefined
          ? {
              requiredQuantity: args.requiredQuantity,
              sufficientForRequired,
            }
          : {}),
      },
    };
  }

  private async resolveWmsSkus(
    wmsCtx: WmsRequestContext,
    skuId?: string,
    partIdentifier?: string,
  ): Promise<Array<{ skuId: string; displayName: string; primaryUom: string }>> {
    if (!skuId?.trim() && !partIdentifier?.trim()) {
      throw new BadRequestException(
        'Either skuId or partIdentifier must be provided.',
      );
    }
    if (skuId?.trim()) {
      const sku = await this.wmsSkuService.getSku(wmsCtx, skuId.trim());
      return [
        {
          skuId: sku.skuId,
          displayName: sku.displayName,
          primaryUom: sku.primaryUom,
        },
      ];
    }
    const matches = await this.wmsSkuService.resolveSkuIdsByQuery(
      wmsCtx,
      partIdentifier!.trim(),
      AI_TOOL_MAX_RECORDS,
    );
    return matches;
  }

  private async runExportTemplateData(
    orgId: string,
    args: ExportTemplateDataArgsDto,
    tenantDatabaseConfigured: boolean,
  ): Promise<AiToolExecutionResult> {
    const limit = Math.min(
      args.limit ?? AI_TOOL_MAX_EXPORT_ROWS,
      AI_TOOL_MAX_EXPORT_ROWS,
    );
    const format = args.format ?? 'json_table';
    const queryLower = args.query?.trim().toLowerCase() ?? '';
    const columnFilter = args.columnFilter?.trim();

    const tpl = await this.templatesService.getOne(orgId, args.templateId);
    const rawCols = Array.isArray(tpl.columns) ? tpl.columns : [];
    const columns: AiExportColumn[] = rawCols.map((c) => {
      const col = c as Record<string, unknown>;
      const name = String(col.name ?? '');
      return { key: name, label: name };
    });

    let rawRows: Array<Record<string, unknown>>;
    let totalCount = 0;

    if (args.includeLinkedData) {
      const enriched = await this.viewDataService.getEnrichedRows(
        orgId,
        args.templateId,
      );
      const filtered = enriched.filter((row) =>
        this.matchesRowQuery(
          row.cells as Record<string, unknown> | undefined,
          String(row.rowId ?? ''),
          queryLower,
          columnFilter,
        ),
      );
      totalCount = filtered.length;
      rawRows = filtered.slice(0, limit);
    } else {
      const res = await this.templatesService.getRows(
        orgId,
        args.templateId,
        1,
        limit,
      );
      const fetched = Array.isArray(res.rows)
        ? (res.rows as Array<Record<string, unknown>>)
        : [];
      const filtered = fetched.filter((row) =>
        this.matchesRowQuery(
          row.cells as Record<string, unknown> | undefined,
          String(row._id ?? ''),
          queryLower,
          columnFilter,
        ),
      );
      totalCount = Number(res.total ?? filtered.length);
      rawRows = filtered.slice(0, limit);
    }

    const exportRows = rawRows.map((row) => {
      const cells = (row.cells as Record<string, unknown>) ?? {};
      const out: Record<string, string> = {};
      for (const col of columns) {
        out[col.key] = String(cells[col.key] ?? '');
      }
      return out;
    });

    const payload: AiStructuredExportPayload = {
      exportType: 'template_data',
      format,
      fileNameSuggestion: sanitizeFileName(
        `template-${String(tpl.name ?? args.templateId)}`,
        format,
      ),
      columns,
      rows: exportRows,
      ...(format === 'csv'
        ? { csvContent: buildFormulaSafeCsv(columns, exportRows) }
        : {}),
      rowCount: exportRows.length,
      truncated: totalCount > exportRows.length,
    };

    return {
      ok: true,
      tool: 'export_template_data',
      dataSource: 'central_mongodb',
      returnedCount: exportRows.length,
      truncated: payload.truncated,
      tenantDatabaseNote:
        exportRows.length === 0 && tenantDatabaseConfigured
          ? TENANT_TEMPLATES_NOTE
          : undefined,
      data: payload,
    };
  }

  private async runExportVendorComparison(
    orgId: string,
    args: ExportVendorComparisonArgsDto,
    tenantDatabaseConfigured: boolean,
  ): Promise<AiToolExecutionResult> {
    const limit = Math.min(
      args.limit ?? AI_TOOL_MAX_RECORDS,
      AI_TOOL_MAX_RECORDS,
    );
    const format = args.format ?? 'json_table';
    const summary = await extractVendorPriceOffers(
      {
        templatesService: this.templatesService,
        viewDataService: this.viewDataService,
        partsService: this.partsService,
      },
      orgId,
      {
        partIdentifier: args.partIdentifier,
        templateId: args.templateId,
        partColumn: args.partColumn,
        vendorColumn: args.vendorColumn,
        priceColumn: args.priceColumn,
        currency: args.currency,
      },
    );

    const bounded = summary.offers.slice(0, limit);
    const columns: AiExportColumn[] = [
      { key: 'partIdentifier', label: 'Part Identifier' },
      { key: 'vendor', label: 'Vendor' },
      { key: 'price', label: 'Price' },
      { key: 'currency', label: 'Currency' },
      { key: 'sourceTemplateName', label: 'Source Template' },
    ];
    const exportRows = bounded.map((o) => ({
      partIdentifier: summary.partIdentifier,
      vendor: o.vendor,
      price: String(o.price),
      currency: o.currency,
      sourceTemplateName: o.sourceTemplateName || o.sourceTemplateId,
    }));

    const payload: AiStructuredExportPayload = {
      exportType: 'vendor_comparison',
      format,
      fileNameSuggestion: sanitizeFileName(
        `vendor-comparison-${summary.partIdentifier}`,
        format,
      ),
      columns,
      rows: exportRows,
      ...(format === 'csv'
        ? { csvContent: buildFormulaSafeCsv(columns, exportRows) }
        : {}),
      rowCount: exportRows.length,
      truncated: summary.offers.length > limit,
      metadata: {
        partIdentifier: summary.partIdentifier,
        comparable: summary.comparable,
        currency: summary.currency,
        currenciesFound: summary.currenciesFound,
        lowestPrice: summary.lowestPrice,
        highestPrice: summary.highestPrice,
        ignoredInvalidCount: summary.ignoredInvalidCount,
      },
    };

    return {
      ok: true,
      tool: 'export_vendor_comparison',
      dataSource: 'central_mongodb',
      returnedCount: exportRows.length,
      truncated: payload.truncated,
      tenantDatabaseNote:
        exportRows.length === 0 && tenantDatabaseConfigured
          ? TENANT_TEMPLATES_NOTE
          : undefined,
      data: payload,
    };
  }

  private async runExportMissingValuesReport(
    orgId: string,
    args: ExportMissingValuesReportArgsDto,
    tenantDatabaseConfigured: boolean,
  ): Promise<AiToolExecutionResult> {
    const limit = Math.min(
      args.limit ?? AI_TOOL_MAX_EXPORT_ROWS,
      AI_TOOL_MAX_EXPORT_ROWS,
    );
    const format = args.format ?? 'json_table';
    const result = await this.templatesService.findMissingValues(
      orgId,
      args.templateId,
      {
        columnIds: args.columnIds,
        columnNames: args.columnNames,
        missingCriteria: args.missingCriteria,
        page: 1,
        limit,
      },
    );

    const columns: AiExportColumn[] = [
      { key: 'rowId', label: 'Row ID' },
      { key: 'missingColumns', label: 'Missing Columns' },
      ...result.columnsAnalyzed.map((c) => ({ key: c.name, label: c.name })),
    ];
    const exportRows = result.rows.map((r) => {
      const row: Record<string, string> = {
        rowId: r.rowId,
        missingColumns: r.missingColumns.join('; '),
      };
      for (const col of result.columnsAnalyzed) {
        row[col.name] = String(r.cells[col.name] ?? '');
      }
      return row;
    });

    const payload: AiStructuredExportPayload = {
      exportType: 'missing_values_report',
      format,
      fileNameSuggestion: sanitizeFileName(
        `missing-values-${result.templateName || args.templateId}`,
        format,
      ),
      columns,
      rows: exportRows,
      ...(format === 'csv'
        ? { csvContent: buildFormulaSafeCsv(columns, exportRows) }
        : {}),
      rowCount: exportRows.length,
      truncated: result.totalAffectedRows > exportRows.length,
      metadata: {
        templateId: result.templateId,
        templateName: result.templateName,
        totalAffectedRows: result.totalAffectedRows,
        missingCountsByColumn: result.missingCountsByColumn,
      },
    };

    return {
      ok: true,
      tool: 'export_missing_values_report',
      dataSource: 'central_mongodb',
      returnedCount: exportRows.length,
      truncated: payload.truncated,
      tenantDatabaseNote:
        result.totalAffectedRows === 0 && tenantDatabaseConfigured
          ? TENANT_TEMPLATES_NOTE
          : undefined,
      data: payload,
    };
  }

  private async runExportDuplicateRecordsReport(
    orgId: string,
    args: ExportDuplicateRecordsReportArgsDto,
    tenantDatabaseConfigured: boolean,
  ): Promise<AiToolExecutionResult> {
    const limit = Math.min(
      args.limit ?? AI_TOOL_MAX_EXPORT_ROWS,
      AI_TOOL_MAX_EXPORT_ROWS,
    );
    const format = args.format ?? 'json_table';
    const result = await this.templatesService.findDuplicateRecords(
      orgId,
      args.templateId,
      {
        columnIds: args.columnIds,
        columnNames: args.columnNames,
        caseSensitive: args.caseSensitive,
        ignoreEmpty: args.ignoreEmpty,
        page: 1,
        limit,
      },
    );

    const columns: AiExportColumn[] = [
      ...result.keyColumns.map((c) => ({ key: c.name, label: c.name })),
      { key: 'duplicateCount', label: 'Duplicate Count' },
      { key: 'rowIds', label: 'Row IDs' },
    ];
    const exportRows = result.duplicateGroups.map((g) => {
      const row: Record<string, string> = {};
      for (const col of result.keyColumns) {
        row[col.name] = String(g.keyValues[col.name] ?? '');
      }
      row.duplicateCount = String(g.duplicateCount);
      row.rowIds = g.rowIds.join('; ');
      return row;
    });

    const payload: AiStructuredExportPayload = {
      exportType: 'duplicate_records_report',
      format,
      fileNameSuggestion: sanitizeFileName(
        `duplicate-records-${result.templateName || args.templateId}`,
        format,
      ),
      columns,
      rows: exportRows,
      ...(format === 'csv'
        ? { csvContent: buildFormulaSafeCsv(columns, exportRows) }
        : {}),
      rowCount: exportRows.length,
      truncated: result.totalDuplicateGroups > exportRows.length,
      metadata: {
        templateId: result.templateId,
        templateName: result.templateName,
        totalDuplicateGroups: result.totalDuplicateGroups,
      },
    };

    return {
      ok: true,
      tool: 'export_duplicate_records_report',
      dataSource: 'central_mongodb',
      returnedCount: exportRows.length,
      truncated: payload.truncated,
      tenantDatabaseNote:
        result.totalDuplicateGroups === 0 && tenantDatabaseConfigured
          ? TENANT_TEMPLATES_NOTE
          : undefined,
      data: payload,
    };
  }

  private sanitizePart(part: Record<string, unknown>) {
    return {
      id: String(part._id ?? ''),
      templateId: String(part.templateId ?? ''),
      rowId: String(part.rowId ?? ''),
      metadata: (part.metadata as Record<string, unknown>) ?? {},
    };
  }

  private formatValidationErrors(errors: ValidationError[]): string {
    return errors
      .map((err) => {
        const constraints = err.constraints
          ? Object.values(err.constraints).join('; ')
          : 'invalid value';
        return `${err.property}: ${constraints}`;
      })
      .join(' | ');
  }
}
