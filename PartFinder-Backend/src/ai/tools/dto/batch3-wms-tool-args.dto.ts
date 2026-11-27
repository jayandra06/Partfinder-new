import { Type } from 'class-transformer';
import {
  IsBoolean,
  IsInt,
  IsNotEmpty,
  IsNumber,
  IsOptional,
  IsString,
  Max,
  MaxLength,
  Min,
} from 'class-validator';
import { AI_TOOL_MAX_RECORDS } from '../ai-tool.types';

export class GetPartInventoryArgsDto {
  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  skuId?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(200)
  partIdentifier?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  warehouseId?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  ownerId?: string;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(AI_TOOL_MAX_RECORDS)
  limit?: number;
}

export class FindPartWarehouseArgsDto {
  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  skuId?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(200)
  partIdentifier?: string;

  @IsOptional()
  @IsBoolean()
  onlyAvailable?: boolean;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(AI_TOOL_MAX_RECORDS)
  limit?: number;
}

export class GetWarehouseDetailsArgsDto {
  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  warehouseId?: string;

  @IsOptional()
  @IsBoolean()
  activeOnly?: boolean;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(AI_TOOL_MAX_RECORDS)
  limit?: number;
}

export class CheckStockAvailabilityArgsDto {
  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  skuId?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(200)
  partIdentifier?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  warehouseId?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  ownerId?: string;

  @IsOptional()
  @Type(() => Number)
  @IsNumber()
  @Min(0)
  requiredQuantity?: number;
}
