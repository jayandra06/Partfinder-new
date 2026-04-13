import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type AdminUserDocument = HydratedDocument<AdminUser>;

@Schema({ timestamps: true, collection: 'admin_users' })
export class AdminUser {
  @Prop({ required: true, unique: true, lowercase: true, trim: true })
  email: string;

  @Prop({ required: true })
  passwordHash: string;

  /** Base32 TOTP secret (same algorithm as authenticator apps). Optional; used for password recovery. */
  @Prop({ required: false })
  totpSecretBase32?: string;

  @Prop({ required: false, default: false })
  totpEnabled?: boolean;
}

export const AdminUserSchema = SchemaFactory.createForClass(AdminUser);