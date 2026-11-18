import { Type } from 'class-transformer';
import {
  IsArray,
  IsBoolean,
  IsEmail,
  IsOptional,
  IsString,
  ValidateNested,
} from 'class-validator';

export class OperatorPermissionsDto {
  @IsOptional()
  @IsBoolean()
  receiving?: boolean;

  @IsOptional()
  @IsBoolean()
  qc?: boolean;

  @IsOptional()
  @IsBoolean()
  putaway?: boolean;

  @IsOptional()
  @IsBoolean()
  supervise?: boolean;
}

export class UpsertOperatorGrantDto {
  @IsEmail()
  email!: string;

  @IsArray()
  @IsString({ each: true })
  warehouseIds!: string[];

  @IsArray()
  @IsString({ each: true })
  ownerIds!: string[];

  @ValidateNested()
  @Type(() => OperatorPermissionsDto)
  permissions!: OperatorPermissionsDto;
}