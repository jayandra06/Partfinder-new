import { Body, Controller, Get, Post } from '@nestjs/common';
import { StartLicenseCheckoutDto } from './dto/start-license-checkout.dto';
import { VerifyLicensePaymentDto } from './dto/verify-license-payment.dto';
import { LicensePurchaseService } from './license-purchase.service';

/** Public self-serve licence checkout (Razorpay). */
@Controller('public/license-purchase')
export class LicensePurchaseController {
  constructor(private readonly licensePurchase: LicensePurchaseService) {}

  /** Whether checkout is enabled and Razorpay credentials are present (no secrets returned). */
  @Get('status')
  status() {
    return this.licensePurchase.getPublicStatus();
  }

  @Post('checkout')
  startCheckout(@Body() dto: StartLicenseCheckoutDto) {
    return this.licensePurchase.startCheckout(dto);
  }

  @Post('verify')
  verifyPayment(@Body() dto: VerifyLicensePaymentDto) {
    return this.licensePurchase.verifyPayment(dto);
  }
}
