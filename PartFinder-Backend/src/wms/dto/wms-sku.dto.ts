import {
  IsIn,
  IsObject,
  IsOptional,
  IsString,
  MaxLength,
  MinLength,
} from 'class-validator';
import { WMS_SKU_NAMESPACES } from '../wms-sku-namespaces';

export class CreateWmsSkuDto {
  @IsString()
  @MinLength(1)
  @MaxLength(120)
  displayName!: string;

  @IsOptional()
  @IsString()
  @MaxLength(500)
  description?: string;

  @IsString()
  @MinLength(1)
  @MaxLength(16)
  primaryUom!: string;

  @IsOptional()
  @IsString()
  @MaxLength(64)
  variantGroupId?: string;

  @IsOptional()
  @IsString()
  @MaxLength(32)
  variantRevision?: string;

  @IsOptional()
  @IsObject()
  dimensions?: Record<string, string>;

  @IsOptional()
  @IsObject()
  packaging?: Record<string, string>;
}

export class UpdateWmsSkuDto {
  @IsOptional()
  @IsString()
  @MinLength(1)
  @MaxLength(120)
  displayName?: string;

  @IsOptional()
  @IsString()
  @MaxLength(500)
  description?: string;

  @IsOptional()
  @IsString()
  @MinLength(1)
  @MaxLength(16)
  primaryUom?: string;

  @IsOptional()
  @IsString()
  @MaxLength(64)
  variantGroupId?: string;

  @IsOptional()
  @IsString()
  @MaxLength(32)
  variantRevision?: string;

  @IsOptional()
  @IsObject()
  dimensions?: Record<string, string>;

  @IsOptional()
  @IsObject()
  packaging?: Record<string, string>;
}

export class AddWmsSkuIdentifierDto {
  @IsString()
  @IsIn([...WMS_SKU_NAMESPACES])
  namespace!: string;

  @IsString()
  @MinLength(1)
  @MaxLength(256)
  value!: string;

  @IsOptional()
  @IsObject()
  metadata?: Record<string, string>;
}

export class MergeWmsSkuDto {
  @IsString()
  @MinLength(1)
  @MaxLength(64)
  mergedIntoSkuId!: string;

  @IsOptional()
  @IsString()
  @MaxLength(500)
  reason?: string;
}

export class RetireWmsSkuDto {
  @IsOptional()
  @IsString()
  @MaxLength(500)
  reason?: string;
}

export class CreateWmsUomDto {
  @IsString()
  @MinLength(1)
  @MaxLength(16)
  code!: string;

  @IsString()
  @MinLength(1)
  @MaxLength(80)
  name!: string;

  @IsOptional()
  @IsString()
  @IsIn(['count', 'mass', 'length', 'volume', 'custom'])
  category?: string;
}

export class CreateWmsUomTemplateMappingDto {
  @IsString()
  @MinLength(1)
  @MaxLength(64)
  templateId!: string;

  /** Raw label from Excel column header or cell (e.g. "Qty (pcs)", "KG"). */
  @IsString()
  @MinLength(1)
  @MaxLength(80)
  sourceLabel!: string;

  @IsString()
  @MinLength(1)
  @MaxLength(16)
  uomCode!: string;
}

export class CreateWmsErpConnectorDto {
  @IsString()
  @MinLength(1)
  @MaxLength(80)
  name!: string;

  @IsString()
  @IsIn(['manual', 'sap', 'oracle', 'dynamics', 'netsuite', 'odoo', 'custom'])
  connectorType!: string;

  @IsOptional()
  @IsString()
  ownerId?: string;
}