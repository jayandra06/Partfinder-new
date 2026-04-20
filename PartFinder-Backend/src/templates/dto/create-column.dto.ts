import { IsIn, IsInt, IsOptional, IsString, MaxLength, Min } from 'class-validator';

export class CreateColumnDto {
  @IsString()
  @MaxLength(80)
  name: string;

  @IsInt()
  @Min(0)
  order: number;

  @IsOptional()
  @IsIn(['text', 'number', 'link'])
  type?: 'text' | 'number' | 'link';
}
