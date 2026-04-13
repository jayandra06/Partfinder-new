import { IsString, MinLength } from 'class-validator';

export class AdminSyncTotpDto {
  @IsString()
  @MinLength(16, { message: 'Invalid authenticator secret' })
  secretBase32: string;
}