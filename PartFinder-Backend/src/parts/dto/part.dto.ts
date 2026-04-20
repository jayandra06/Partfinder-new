import { IsOptional, IsString } from 'class-validator';

export class CreatePartDto {
  @IsString()
  templateId: string;

  @IsString()
  rowId: string;

  @IsOptional()
  metadata?: Record<string, string>;
}

export class UpdatePartDto {
  @IsOptional()
  metadata?: Record<string, string>;
}
