import { IsIn, IsString, Matches, MinLength } from 'class-validator';

export class CreateOrganizationDto {
  @IsString()
  @MinLength(1)
  name: string;

  @IsString()
  @IsIn(['premium', 'standard'])
  type: string;

  @IsString()
  @IsIn(['lifetime', 'annual'])
  plan: string;

  @IsString()
  @Matches(/^\d{6}$/, { message: 'Organization code must be exactly 6 digits' })
  orgCode: string;
}