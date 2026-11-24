import { IsString, Matches, MinLength } from 'class-validator';

export class SetupResetPasswordDto {
  @IsString()
  @MinLength(10)
  token: string;

  @IsString()
  @MinLength(8, { message: 'New password must be at least 8 characters' })
  @Matches(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/, {
    message:
      'New password must include at least one uppercase letter, one lowercase letter, and one number',
  })
  newPassword: string;
}