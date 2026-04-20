import { Type } from 'class-transformer';
import { ArrayMinSize, IsArray, IsOptional, IsString, MaxLength, ValidateNested } from 'class-validator';

export class RowCellDto {
  @IsString()
  columnId: string;

  @IsString()
  @MaxLength(2000)
  value: string;
}

export class CreateRowDto {
  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => RowCellDto)
  cells: RowCellDto[];
}

export class UpdateRowDto {
  @IsOptional()
  @IsArray()
  @ValidateNested({ each: true })
  @Type(() => RowCellDto)
  cells?: RowCellDto[];
}
