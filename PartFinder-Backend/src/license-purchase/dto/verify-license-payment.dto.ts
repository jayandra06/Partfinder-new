import { IsString, MinLength } from 'class-validator';

export class VerifyLicensePaymentDto {
  @IsString()
  @MinLength(1)
  razorpayOrderId: string;

  @IsString()
  @MinLength(1)
  razorpayPaymentId: string;

  @IsString()
  @MinLength(1)
  razorpaySignature: string;
}
