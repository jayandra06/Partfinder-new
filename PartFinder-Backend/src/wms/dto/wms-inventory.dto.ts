import { Type } from 'class-transformer';
import {
  ArrayMinSize,
  IsArray,
  IsBoolean,
  IsIn,
  IsNumber,
  IsOptional,
  IsString,
  MaxLength,
  Min,
  MinLength,
  ValidateNested,
} from 'class-validator';
import { WMS_STOCK_TXN_TYPES } from '../wms-stock-transaction.types';

export class WmsInventoryLineDto {
  @IsString()
  @MinLength(1)
  @MaxLength(64)
  skuId!: string;

  @IsNumber()
  quantity!: number;

  /** Signed delta for `adjust` transactions (overrides `quantity` when set). */
  @IsOptional()
  @IsNumber()
  qtyDelta?: number;

  @IsOptional()
  @IsString()
  fromLocationId?: string;

  @IsOptional()
  @IsString()
  toLocationId?: string;

  @IsOptional()
  @IsString()
  @MaxLength(64)
  lotId?: string;

  @IsOptional()
  @IsString()
  @MaxLength(64)
  serialId?: string;

  @IsOptional()
  @IsString()
  targetOwnerId?: string;
}

export class WmsPostInventoryDto {
  @IsString()
  @IsIn([...WMS_STOCK_TXN_TYPES])
  type!: (typeof WMS_STOCK_TXN_TYPES)[number];

  @IsString()
  @MinLength(8)
  @MaxLength(128)
  idempotencyKey!: string;

  @IsString()
  @MinLength(1)
  ownerId!: string;

  @IsString()
  @MinLength(1)
  warehouseId!: string;

  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => WmsInventoryLineDto)
  lines!: WmsInventoryLineDto[];

  @IsOptional()
  refs?: Record<string, string>;

  @IsOptional()
  @IsString()
  reversalOf?: string;

  @IsOptional()
  @IsString()
  @MaxLength(500)
  approvalNote?: string;

  @IsOptional()
  @IsBoolean()
  emitErpEvent?: boolean;
}

export class WmsErpSkuMappingDto {
  @IsString()
  erpConnectorId!: string;

  @IsString()
  @MinLength(1)
  externalMaterialId!: string;

  @IsString()
  @MinLength(1)
  skuId!: string;
}

export class WmsErpLocationMappingDto {
  @IsString()
  erpConnectorId!: string;

  @IsString()
  @MinLength(1)
  externalLocationId!: string;

  @IsString()
  warehouseId!: string;

  @IsString()
  locationId!: string;
}