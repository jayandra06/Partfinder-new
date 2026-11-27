import { Type } from 'class-transformer';
import {
  ArrayMaxSize,
  IsArray,
  IsBoolean,
  IsIn,
  IsInt,
  IsNotEmpty,
  IsOptional,
  IsString,
  Max,
  MaxLength,
  Min,
} from 'class-validator';
import { AI_TOOL_MAX_RECORDS } from '../ai-tool.types';

export class CompareVendorPricesArgsDto {
  @IsString()
  @IsNotEmpty()
  @MaxLength(200)
  partIdentifier!: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  templateId?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  partColumn?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  vendorColumn?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  priceColumn?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(16)
  currency?: string;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(AI_TOOL_MAX_RECORDS)
  limit?: number;
}

export class FindCheapestVendorArgsDto {
  @IsString()
  @IsNotEmpty()
  @MaxLength(200)
  partIdentifier!: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  templateId?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  partColumn?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  vendorColumn?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  priceColumn?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(16)
  currency?: string;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(AI_TOOL_MAX_RECORDS)
  limit?: number;
}

export class FindMissingValuesArgsDto {
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  templateId!: string;

  @IsOptional()
  @IsArray()
  @ArrayMaxSize(20)
  @IsString({ each: true })
  @IsNotEmpty({ each: true })
  @MaxLength(128, { each: true })
  columnIds?: string[];

  @IsOptional()
  @IsArray()
  @ArrayMaxSize(20)
  @IsString({ each: true })
  @IsNotEmpty({ each: true })
  @MaxLength(128, { each: true })
  columnNames?: string[];

  @IsOptional()
  @IsIn(['blank', 'null_or_empty', 'include_na'])
  missingCriteria?: 'blank' | 'null_or_empty' | 'include_na';

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(1000)
  page?: number;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(AI_TOOL_MAX_RECORDS)
  limit?: number;
}

export class FindDuplicateRecordsArgsDto {
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  templateId!: string;

  @IsOptional()
  @IsArray()
  @ArrayMaxSize(10)
  @IsString({ each: true })
  @IsNotEmpty({ each: true })
  @MaxLength(128, { each: true })
  columnIds?: string[];

  @IsOptional()
  @IsArray()
  @ArrayMaxSize(10)
  @IsString({ each: true })
  @IsNotEmpty({ each: true })
  @MaxLength(128, { each: true })
  columnNames?: string[];

  @IsOptional()
  @IsBoolean()
  caseSensitive?: boolean;

  @IsOptional()
  @IsBoolean()
  ignoreEmpty?: boolean;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(1000)
  page?: number;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(AI_TOOL_MAX_RECORDS)
  limit?: number;
}
