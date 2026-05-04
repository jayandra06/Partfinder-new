import { Transform, Type } from 'class-transformer';
import {
  IsArray,
  IsBoolean,
  IsEmail,
  IsIn,
  IsOptional,
  IsObject,
  IsString,
  Matches,
  ValidateNested,
} from 'class-validator';

export class TemplatePermissionsDto {
  @IsBoolean()
  @IsOptional()
  add?: boolean;

  @IsBoolean()
  @IsOptional()
  view?: boolean;

  @IsBoolean()
  @IsOptional()
  edit?: boolean;

  @IsBoolean()
  @IsOptional()
  delete?: boolean;
}

export class MasterDataPermissionsDto {
  @IsBoolean()
  @IsOptional()
  copy?: boolean;

  @IsBoolean()
  @IsOptional()
  view?: boolean;

  @IsBoolean()
  @IsOptional()
  edit?: boolean;

  @IsBoolean()
  @IsOptional()
  add?: boolean;

  @IsBoolean()
  @IsOptional()
  delete?: boolean;
}

export class SetupInviteUserDto {
  @IsString()
  @Transform(({ value }) => (typeof value === 'string' ? value.trim() : value))
  @Matches(/^\d{6}$/, { message: 'Organization code must be exactly 6 digits' })
  orgCode: string;

  @IsString()
  @Transform(({ value }) => (typeof value === 'string' ? value.trim() : value))
  name: string;

  @IsEmail()
  @Transform(({ value }) =>
    typeof value === 'string' ? value.trim().toLowerCase() : value,
  )
  email: string;

  @IsString()
  @IsIn(['Admin', 'Employee'])
  role: 'Admin' | 'Employee';

  @IsBoolean()
  partsAllTemplates: boolean;

  @IsArray()
  @IsString({ each: true })
  @IsOptional()
  allowedTemplateIds?: string[];

  @IsOptional()
  @IsObject()
  @ValidateNested()
  @Type(() => TemplatePermissionsDto)
  templatePermissions?: TemplatePermissionsDto;

  @IsOptional()
  @IsObject()
  @ValidateNested()
  @Type(() => MasterDataPermissionsDto)
  masterDataPermissions?: MasterDataPermissionsDto;
}
