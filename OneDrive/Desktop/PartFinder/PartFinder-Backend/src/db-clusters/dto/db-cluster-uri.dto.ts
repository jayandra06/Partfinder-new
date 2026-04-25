import { Transform } from 'class-transformer';
import { IsString, MinLength } from 'class-validator';

export class DbClusterUriDto {
  @Transform(({ value }) => (typeof value === 'string' ? value.trim() : value))
  @IsString()
  @MinLength(1, { message: 'MongoDB URI is required' })
  uri: string;
}
