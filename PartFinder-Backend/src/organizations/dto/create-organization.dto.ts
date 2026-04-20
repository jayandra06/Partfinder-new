import { IsEmail, IsIn, IsString, Matches, MinLength } from 'class-validator';
import { Transform } from 'class-transformer';
import { ORG_PLANS, ORG_TYPES } from '../org.constants';

export class CreateOrganizationDto {
  @IsString()
  @MinLength(1)
  name: string;

  @IsString()
  @IsIn([...ORG_TYPES])
  type: string;

  @IsString()
  @IsIn([...ORG_PLANS])
  plan: string;

  @IsString()
  @Matches(/^\d{6}$/, { message: 'Organization code must be exactly 6 digits' })
  orgCode: string;

  @IsEmail()
  @Transform(({ value }) => (typeof value === 'string' ? value.trim().toLowerCase() : value))
  firstAdminEmail: string;
}