import {
  IsBoolean,
  IsIn,
  IsMongoId,
  IsNotEmpty,
  IsOptional,
  IsString,
  MaxLength,
} from 'class-validator';

const LOCATION_TYPES = [
  'receiving',
  'storage',
  'staging',
  'dispatch',
] as const;

export class CreateWmsLocationDto {
  @IsMongoId()
  warehouseId!: string;

  @IsOptional()
  @IsMongoId()
  zoneId?: string;

  @IsString()
  @IsNotEmpty()
  @MaxLength(120)
  path!: string;

  @IsString()
  @IsIn(LOCATION_TYPES)
  type!: (typeof LOCATION_TYPES)[number];

  @IsOptional()
  @IsBoolean()
  isPickable?: boolean;

  @IsOptional()
  @IsBoolean()
  isActive?: boolean;
}

export class UpdateWmsLocationDto {
  @IsOptional()
  @IsMongoId()
  zoneId?: string;

  @IsOptional()
  @IsString()
  @MaxLength(120)
  path?: string;

  @IsOptional()
  @IsString()
  @IsIn(LOCATION_TYPES)
  type?: (typeof LOCATION_TYPES)[number];

  @IsOptional()
  @IsBoolean()
  isPickable?: boolean;

  @IsOptional()
  @IsBoolean()
  isActive?: boolean;
}