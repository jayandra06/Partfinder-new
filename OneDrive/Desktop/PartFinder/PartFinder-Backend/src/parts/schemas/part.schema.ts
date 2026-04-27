import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type PartDocument = HydratedDocument<Part>;

@Schema({ timestamps: true, collection: 'parts' })
export class Part {
  @Prop({ required: true, index: true })
  orgId: string;

  @Prop({ required: true, index: true })
  templateId: string;

  @Prop({ required: true, index: true })
  rowId: string;

  @Prop({ type: Object, default: {} })
  metadata: Record<string, string>;
}

export const PartSchema = SchemaFactory.createForClass(Part);
PartSchema.index({ orgId: 1, templateId: 1 });
