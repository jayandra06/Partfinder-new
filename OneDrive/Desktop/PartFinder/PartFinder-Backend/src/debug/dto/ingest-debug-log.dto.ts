import { IsIn, IsOptional, IsString } from 'class-validator';

export class IngestDebugLogDto {
  @IsOptional()
  @IsIn(['partfinder-desktop', 'portfolio'])
  source?: 'partfinder-desktop' | 'portfolio';

  @IsString()
  level!: string;

  @IsString()
  message!: string;

  @IsOptional()
  @IsString()
  context?: string;
}
