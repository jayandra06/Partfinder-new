import { IsIn, IsOptional, IsString, MaxLength, MinLength } from 'class-validator';

export class UpdateColumnDto {
  @IsOptional()
  @IsString()
  @MinLength(1)
  @MaxLength(80)
  name?: string;

  @IsOptional()
  @IsIn(['text', 'number', 'link'])
  type?: 'text' | 'number' | 'link';
}
