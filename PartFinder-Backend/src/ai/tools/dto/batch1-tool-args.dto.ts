import { Type } from 'class-transformer';
import {
  IsBoolean,
  IsInt,
  IsNotEmpty,
  IsOptional,
  IsString,
  Max,
  MaxLength,
  Min,
} from 'class-validator';
import { AI_TOOL_MAX_RECORDS } from '../ai-tool.types';

export class GetTemplateSchemaArgsDto {
  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  templateId?: string;

  @IsOptional()
  @IsBoolean()
  includeColumns?: boolean;
}

export class SearchTemplateRowsArgsDto {
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  templateId!: string;

  @IsOptional()
  @IsString()
  @MaxLength(200)
  query?: string;

  @IsOptional()
  @IsString()
  @MaxLength(128)
  columnFilter?: string;

  @IsOptional()
  @IsBoolean()
  includeLinkedData?: boolean;

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

export class GetTemplateRelationshipsArgsDto {
  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  templateId?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  relationId?: string;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(AI_TOOL_MAX_RECORDS)
  limit?: number;
}

export class GetPartDetailsArgsDto {
  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  partId?: string;

  @IsOptional()
  @IsString()
  @IsNotEmpty()
  @MaxLength(128)
  templateId?: string;

  @IsOptional()
  @IsString()
  @MaxLength(200)
  search?: string;

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
