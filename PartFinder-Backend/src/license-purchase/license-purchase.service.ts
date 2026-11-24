import {
  BadRequestException,
  Injectable,
  InternalServerErrorException,
  ServiceUnavailableException,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { InjectModel } from '@nestjs/mongoose';
import Razorpay from 'razorpay';
import { verifyRazorpayCheckoutSignature } from './license-purchase.signature';
import { Model } from 'mongoose';
import { OrganizationsService } from '../organizations/organizations.service';
import { StartLicenseCheckoutDto } from './dto/start-license-checkout.dto';
import { VerifyLicensePaymentDto } from './dto/verify-license-payment.dto';
import {
  isLicensePurchaseEnabled,
  isRazorpayConfigured,
  isRazorpayTestKeyId,
  razorpayConfigurationMessage,
  readRazorpayKeys,
} from './license-purchase.config';
import {
  LicensePurchase,
  LicensePurchaseDocument,
} from './schemas/license-purchase.schema';

/**
 * PartFinder self-serve licence: $10.00 USD.
 * Razorpay `amount` for USD is in cents (1000 = $10.00). Do not use INR paise semantics.
 */
export const LICENSE_PRICE_USD_CENTS = 1000;

@Injectable()
export class LicensePurchaseService {
  constructor(
    @InjectModel(LicensePurchase.name)
    private readonly purchaseModel: Model<LicensePurchaseDocument>,
    private readonly organizations: OrganizationsService,
    private readonly config: ConfigService,
  ) {}

  getPublicStatus() {
    const enabled = isLicensePurchaseEnabled(this.config);
    const configured = isRazorpayConfigured(this.config);
    const { keyId } = readRazorpayKeys(this.config);
    const configMessage = razorpayConfigurationMessage(this.config);
    return {
      ok: true as const,
      enabled,
      configured,
      ready: enabled && configured,
      /** Public Razorpay Key ID only — never expose RAZORPAY_KEY_SECRET to clients. */
      keyId: configured ? keyId : null,
      testMode: configured ? isRazorpayTestKeyId(keyId) : null,
      amount: LICENSE_PRICE_USD_CENTS,
      currency: 'USD',
      priceLabel: '$10 USD',
      message: configMessage,
    };
  }

  async startCheckout(dto: StartLicenseCheckoutDto) {
    this.assertCheckoutAvailable();
    const { keyId } = readRazorpayKeys(this.config);
    const razorpay = this.razorpayClient();

    const organisationName = dto.organisationName.trim();
    const email = dto.email.trim().toLowerCase();
    const mobile = dto.mobile.trim();

    const receipt = `pf-lic-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
    let order: { id: string };
    try {
      order = (await razorpay.orders.create({
        amount: LICENSE_PRICE_USD_CENTS,
        currency: 'USD',
        receipt,
        notes: {
          organisationName,
          email,
          mobile,
        },
      })) as { id: string };
    } catch (error) {
      throw this.mapRazorpayError(error);
    }

    const orderId = String(order.id);
    await this.purchaseModel.create({
      organisationName,
      email,
      mobile,
      razorpayOrderId: orderId,
      razorpayPaymentId: null,
      status: 'pending',
      orgCode: null,
      temporaryPassword: null,
      amountCents: LICENSE_PRICE_USD_CENTS,
      currency: 'USD',
    });

    return {
      ok: true as const,
      keyId,
      orderId,
      amount: LICENSE_PRICE_USD_CENTS,
      currency: 'USD',
      description: 'PartFinder organisation licence',
      prefill: {
        name: organisationName,
        email,
        contact: mobile.replace(/\D/g, ''),
      },
    };
  }

  async verifyPayment(dto: VerifyLicensePaymentDto) {
    this.assertCheckoutAvailable();
    const { keySecret } = readRazorpayKeys(this.config);
    const orderId = dto.razorpayOrderId.trim();
    const paymentId = dto.razorpayPaymentId.trim();
    const signature = dto.razorpaySignature.trim();

    if (!verifyRazorpayCheckoutSignature(orderId, paymentId, signature, keySecret)) {
      throw new BadRequestException(
        'Payment signature verification failed. The licence was not activated.',
      );
    }

    const purchase = await this.purchaseModel.findOne({ razorpayOrderId: orderId }).exec();
    if (!purchase) {
      throw new BadRequestException('Unknown payment order.');
    }

    if (purchase.status === 'fulfilled' && purchase.orgCode && purchase.temporaryPassword) {
      return {
        ok: true as const,
        organisationName: purchase.organisationName,
        email: purchase.email,
        orgCode: purchase.orgCode,
        temporaryPassword: purchase.temporaryPassword,
      };
    }

    await this.assertPaymentCaptured(paymentId, orderId);

    if (purchase.status === 'pending') {
      purchase.status = 'paid';
      purchase.razorpayPaymentId = paymentId;
      await purchase.save();
    }

    if (purchase.status === 'fulfilled' && purchase.orgCode && purchase.temporaryPassword) {
      return {
        ok: true as const,
        organisationName: purchase.organisationName,
        email: purchase.email,
        orgCode: purchase.orgCode,
        temporaryPassword: purchase.temporaryPassword,
      };
    }

    if (!purchase.orgCode || !purchase.temporaryPassword) {
      const provisioned = await this.organizations.createSelfServeLicense({
        name: purchase.organisationName,
        firstAdminEmail: purchase.email,
        plan: 'starter',
        type: 'standard',
      });
      purchase.status = 'fulfilled';
      purchase.orgCode = provisioned.orgCode;
      purchase.temporaryPassword = provisioned.temporaryPassword;
      await purchase.save();
    }

    return {
      ok: true as const,
      organisationName: purchase.organisationName,
      email: purchase.email,
      orgCode: purchase.orgCode!,
      temporaryPassword: purchase.temporaryPassword!,
    };
  }

  private assertCheckoutAvailable() {
    const message = razorpayConfigurationMessage(this.config);
    if (message) {
      throw new ServiceUnavailableException(message);
    }
  }

  private razorpayClient() {
    const { keyId, keySecret } = readRazorpayKeys(this.config);
    return new Razorpay({ key_id: keyId, key_secret: keySecret });
  }

  private mapRazorpayError(error: unknown): BadRequestException | InternalServerErrorException {
    if (error && typeof error === 'object') {
      const err = error as {
        statusCode?: number;
        error?: { description?: string; reason?: string; code?: string };
        description?: string;
      };
      if (err.statusCode === 401) {
        return new BadRequestException(
          'Razorpay rejected the API credentials. The Key Secret in PartFinder-Backend/.env must be the secret shown for the same Test Mode Key ID (Dashboard → API Keys → Reveal). Random or placeholder secrets will not work.',
        );
      }
      const message =
        err.error?.description ||
        err.error?.reason ||
        err.description ||
        '';
      if (message) {
        const lower = message.toLowerCase();
        if (lower.includes('currency') || err.error?.code === 'BAD_REQUEST_ERROR') {
          return new BadRequestException(
            lower.includes('currency')
              ? `${message} PartFinder charges $10 USD — enable USD / international payments on your Razorpay account before checkout.`
              : message,
          );
        }
        return new BadRequestException(message);
      }
    }
    return new InternalServerErrorException(
      'Could not create a Razorpay order. Check API credentials and that your Razorpay account supports USD for this amount ($10.00).',
    );
  }

  private async assertPaymentCaptured(paymentId: string, orderId: string) {
    const razorpay = this.razorpayClient();
    let payment: {
      status?: string;
      order_id?: string;
      amount?: number;
      currency?: string;
    };
    try {
      payment = (await razorpay.payments.fetch(paymentId)) as typeof payment;
    } catch {
      throw new BadRequestException(
        'Could not confirm payment with Razorpay. The licence was not activated.',
      );
    }
    if (payment.status !== 'captured' && payment.status !== 'authorized') {
      throw new BadRequestException(
        `Payment was not completed (status: ${payment.status ?? 'unknown'}). The licence was not activated.`,
      );
    }
    if (payment.order_id !== orderId) {
      throw new BadRequestException(
        'Payment does not match this checkout order. The licence was not activated.',
      );
    }
    if (
      payment.amount !== LICENSE_PRICE_USD_CENTS ||
      (payment.currency ?? '').toUpperCase() !== 'USD'
    ) {
      throw new BadRequestException(
        `Payment amount or currency does not match the $10 USD licence price (got ${payment.amount ?? '?'} ${payment.currency ?? '?'}). The licence was not activated.`,
      );
    }
  }
}
