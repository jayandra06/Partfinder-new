import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type TemplateColumnDocument = HydratedDocument<TemplateColumn>;

@Schema({ timestamps: true, collection: 'template_columns' })
export class TemplateColumn {
  @Prop({ required: true, index: true })
  templateId: string;

  @Prop({ required: true, trim: true })
  name: string;

  @Prop({ required: true })
  order: number;

  @Prop({ required: true, default: false })
  isLink: boolean;

  @Prop({ required: true, enum: ['text', 'number', 'link'], default: 'text' })
  type: 'text' | 'number' | 'link';
}

export const TemplateColumnSchema = SchemaFactory.createForClass(TemplateColumn);
TemplateColumnSchema.index({ templateId: 1, order: 1 });
