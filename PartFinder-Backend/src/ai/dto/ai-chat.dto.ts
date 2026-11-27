import { Type } from 'class-transformer';
import {
  IsNotEmpty,
  IsOptional,
  IsString,
  MaxLength,
  ValidateNested,
} from 'class-validator';

export class AiAppContextDto {
  @IsOptional()
  @IsString()
  @MaxLength(64)
  currentPage?: string;

  @IsOptional()
  @IsString()
  @MaxLength(128)
  selectedTemplateId?: string;

  @IsOptional()
  @IsString()
  @MaxLength(128)
  selectedCatalogId?: string;
}

export class AiChatRequestDto {
  @IsOptional()
  @IsString()
  @MaxLength(64)
  conversationId?: string;

  @IsString()
  @IsNotEmpty()
  @MaxLength(4000)
  message!: string;

  @IsOptional()
  @ValidateNested()
  @Type(() => AiAppContextDto)
  appContext?: AiAppContextDto;
}
