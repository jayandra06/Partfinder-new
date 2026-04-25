import { Type } from 'class-transformer';
import {
  ArrayMinSize,
  IsArray,
  IsIn,
  IsOptional,
  IsString,
  ValidateNested,
} from 'class-validator';

export class DebugLogItemDto {
  @IsIn(['portfolio', 'partfinder-desktop'])
  source!: 'portfolio' | 'partfinder-desktop';

  @IsString()
  level!: string;

  @IsString()
  message!: string;

  @IsOptional()
  @IsString()
  context?: string;
}

export class AppendDebugLogsDto {
  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => DebugLogItemDto)
  items!: DebugLogItemDto[];
}
