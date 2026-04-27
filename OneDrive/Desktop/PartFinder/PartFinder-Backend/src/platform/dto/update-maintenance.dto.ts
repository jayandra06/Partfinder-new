import { IsBoolean, IsISO8601, IsOptional } from 'class-validator';

export class UpdateMaintenanceDto {
  @IsBoolean()
  maintenanceEnabled!: boolean;

  /** ISO 8601 end time; required when maintenanceEnabled is true (validated in service). */
  @IsOptional()
  @IsISO8601()
  maintenanceUntil?: string;
}
