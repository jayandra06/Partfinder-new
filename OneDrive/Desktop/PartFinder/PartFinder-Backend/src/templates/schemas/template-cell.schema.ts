import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type TemplateCellDocument = HydratedDocument<TemplateCell>;

@Schema({ timestamps: true, collection: 'template_cells' })
export class TemplateCell {
  @Prop({ required: true, index: true })
  rowId: string;

  @Prop({ required: true, index: true })
  columnId: string;

  @Prop({ required: true, default: '' })
  value: string;
}

export const TemplateCellSchema = SchemaFactory.createForClass(TemplateCell);
TemplateCellSchema.index({ rowId: 1, columnId: 1 }, { unique: true });
