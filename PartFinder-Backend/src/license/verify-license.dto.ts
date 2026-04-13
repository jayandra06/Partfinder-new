import { Transform } from 'class-transformer';
import { IsString, Matches } from 'class-validator';

export class VerifyLicenseDto {
  @IsString()
  @Transform(({ value }) => (typeof value === 'string' ? value.trim() : value))
  @Matches(/^\d{6}$/, { message: 'Organization code must be exactly 6 digits' })
  orgCode: string;
}