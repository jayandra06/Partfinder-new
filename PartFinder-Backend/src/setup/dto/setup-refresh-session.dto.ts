import { Transform } from 'class-transformer';
import { IsEmail, IsOptional, IsString, Matches, MinLength } from 'class-validator';

export class SetupRefreshSessionDto {
  @IsString()
  @Transform(({ value }) => (typeof value === 'string' ? value.trim() : value))
  @Matches(/^\d{6}$/, { message: 'Organization code must be exactly 6 digits' })
  orgCode: string;

  @IsEmail()
  @Transform(({ value }) =>
    typeof value === 'string' ? value.trim().toLowerCase() : value,
  )
  email: string;

  @IsString()
  @MinLength(8)
  deviceId: string;

  @IsString()
  @MinLength(16)
  refreshToken: string;
}

export class SetupRevokeSessionDto {
  @IsString()
  @Transform(({ value }) => (typeof value === 'string' ? value.trim() : value))
  @Matches(/^\d{6}$/, { message: 'Organization code must be exactly 6 digits' })
  orgCode: string;

  @IsEmail()
  @Transform(({ value }) =>
    typeof value === 'string' ? value.trim().toLowerCase() : value,
  )
  email: string;

  @IsString()
  @MinLength(8)
  deviceId: string;

  @IsOptional()
  @IsString()
  @MinLength(16)
  refreshToken?: string;
}