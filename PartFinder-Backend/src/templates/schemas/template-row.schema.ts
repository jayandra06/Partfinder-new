import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type TemplateRowDocument = HydratedDocument<TemplateRow>;

@Schema({ timestamps: true, collection: 'template_rows' })
export class TemplateRow {
  @Prop({ required: true, index: true })
  templateId: string;

  @Prop({ required: true, index: true })
  orgId: string;
}

export const TemplateRowSchema = SchemaFactory.createForClass(TemplateRow);
TemplateRowSchema.index({ orgId: 1, templateId: 1 });
