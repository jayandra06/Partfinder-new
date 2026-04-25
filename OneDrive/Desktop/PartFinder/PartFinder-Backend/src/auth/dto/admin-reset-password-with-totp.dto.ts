import { IsEmail, IsString, Matches, MinLength } from 'class-validator';

export class AdminResetPasswordWithTotpDto {
  @IsEmail()
  email: string;

  @IsString()
  @Matches(/^\d{6}$/, { message: 'Code must be 6 digits' })
  totpCode: string;

  @IsString()
  @MinLength(8, { message: 'New password must be at least 8 characters' })
  newPassword: string;
}