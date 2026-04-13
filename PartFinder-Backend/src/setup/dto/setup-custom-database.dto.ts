import { Transform, Type } from 'class-transformer';
import { IsBoolean, IsOptional, IsString, Matches, MinLength } from 'class-validator';

export class SetupCustomDatabaseDto {
  @IsString()
  @Transform(({ value }) => (typeof value === 'string' ? value.trim() : value))
  @Matches(/^\d{6}$/, { message: 'Organization code must be exactly 6 digits' })
  orgCode: string;

  @IsString()
  @MinLength(1)
  @Transform(({ value }) => (typeof value === 'string' ? value.trim() : value))
  uri: string;

  @IsOptional()
  @Type(() => Boolean)
  @IsBoolean()
  /** When true, server skips tenant init (use only if desktop already initialized LAN-only DB). */
  clientInitializationConfirmed?: boolean;
}
