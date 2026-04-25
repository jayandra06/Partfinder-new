import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type TemplateDocument = HydratedDocument<Template>;

@Schema({ timestamps: true, collection: 'templates' })
export class Template {
  @Prop({ required: true, index: true })
  orgId: string;

  @Prop({ required: true, trim: true })
  name: string;
}

export const TemplateSchema = SchemaFactory.createForClass(Template);
TemplateSchema.index({ orgId: 1, name: 1 }, { unique: true });
