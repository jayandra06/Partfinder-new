import { Type } from 'class-transformer';
import { IsBoolean, IsInt, IsOptional, Min } from 'class-validator';

export class BanLicenseDto {
  @IsOptional()
  @IsBoolean()
  permanent?: boolean;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(0)
  minutes?: number;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(0)
  hours?: number;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(0)
  days?: number;
}