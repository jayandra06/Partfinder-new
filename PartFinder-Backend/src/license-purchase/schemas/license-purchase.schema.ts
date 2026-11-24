import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type LicensePurchaseDocument = HydratedDocument<LicensePurchase>;

export type LicensePurchaseStatus = 'pending' | 'paid' | 'fulfilled' | 'failed';

@Schema({ timestamps: true, collection: 'license_purchases' })
export class LicensePurchase {
  @Prop({ required: true })
  organisationName: string;

  @Prop({ required: true })
  email: string;

  @Prop({ required: true })
  mobile: string;

  @Prop({ required: true, unique: true })
  razorpayOrderId: string;

  @Prop({ type: String, default: null })
  razorpayPaymentId: string | null;

  @Prop({ required: true, default: 'pending' })
  status: LicensePurchaseStatus;

  @Prop({ type: String, default: null })
  orgCode: string | null;

  @Prop({ type: String, default: null })
  temporaryPassword: string | null;

  @Prop({ required: true })
  amountCents: number;

  @Prop({ required: true, default: 'USD' })
  currency: string;
}

export const LicensePurchaseSchema = SchemaFactory.createForClass(LicensePurchase);
