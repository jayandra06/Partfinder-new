import { Type } from 'class-transformer';
import { ArrayMinSize, IsArray, IsOptional, IsString, MaxLength, MinLength, ValidateNested } from 'class-validator';

export class RelationMatchPairDto {
  @IsString()
  sourceColumn: string;

  @IsString()
  targetColumn: string;
}

export class CreateRelationDto {
  @IsString()
  primaryTemplateId: string;

  @IsString()
  lookupTemplateId: string;

  @IsString()
  @MinLength(1)
  @MaxLength(100)
  name: string;

  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => RelationMatchPairDto)
  matchKeys: RelationMatchPairDto[];

  @IsArray()
  @IsString({ each: true })
  displayColumns: string[];

  @IsString()
  triggerColumn: string;

  @IsString()
  @MinLength(1)
  @MaxLength(60)
  menuLabel: string;
}

export class UpdateRelationDto {
  @IsOptional()
  @IsString()
  @MinLength(1)
  @MaxLength(100)
  name?: string;

  @IsOptional()
  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => RelationMatchPairDto)
  matchKeys?: RelationMatchPairDto[];

  @IsOptional()
  @IsArray()
  @IsString({ each: true })
  displayColumns?: string[];

  @IsOptional()
  @IsString()
  triggerColumn?: string;

  @IsOptional()
  @IsString()
  @MinLength(1)
  @MaxLength(60)
  menuLabel?: string;
}
