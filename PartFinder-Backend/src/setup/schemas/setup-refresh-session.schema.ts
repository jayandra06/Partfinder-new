import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type SetupRefreshSessionDocument = HydratedDocument<SetupRefreshSession>;

@Schema({ collection: 'setup_refresh_sessions', timestamps: true })
export class SetupRefreshSession {
  @Prop({ required: true, index: true })
  orgCode!: string;

  @Prop({ required: true, index: true })
  emailNormalized!: string;

  @Prop({ required: true, index: true })
  deviceId!: string;

  @Prop({ required: true, unique: true })
  sessionId!: string;

  @Prop({ required: true })
  refreshTokenHash!: string;

  @Prop({ type: String, default: null })
  previousRefreshTokenHash?: string | null;

  @Prop({ default: true })
  isActive!: boolean;

  @Prop({ required: true })
  expiresAt!: Date;
}

export const SetupRefreshSessionSchema =
  SchemaFactory.createForClass(SetupRefreshSession);

SetupRefreshSessionSchema.index(
  { orgCode: 1, emailNormalized: 1, deviceId: 1 },
  { unique: true },
);