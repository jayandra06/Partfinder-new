import { IsIn, IsOptional, IsString, MinLength } from 'class-validator';
import { ORG_PLANS, ORG_STATUSES, ORG_TYPES } from '../org.constants';

export class UpdateOrganizationDto {
  @IsOptional()
  @IsString()
  @MinLength(1)
  name?: string;

  @IsOptional()
  @IsIn([...ORG_TYPES])
  type?: string;

  @IsOptional()
  @IsIn([...ORG_PLANS])
  plan?: string;

  @IsOptional()
  @IsIn([...ORG_STATUSES])
  status?: string;
}