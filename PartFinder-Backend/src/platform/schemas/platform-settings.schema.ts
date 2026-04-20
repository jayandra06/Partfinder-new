import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type PlatformSettingsDocument = HydratedDocument<PlatformSettings>;

@Schema({ collection: 'platform_settings' })
export class PlatformSettings {
  /** Single document key. */
  @Prop({ required: true, unique: true, default: 'singleton' })
  key: string;

  @Prop({ default: false })
  maintenanceEnabled: boolean;

  @Prop({ type: Date, default: null })
  maintenanceUntil: Date | null;
}

export const PlatformSettingsSchema =
  SchemaFactory.createForClass(PlatformSettings);
