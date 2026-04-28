import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type RelationDisplayColumnDocument = HydratedDocument<RelationDisplayColumn>;

@Schema({ timestamps: true, collection: 'relation_display_columns' })
export class RelationDisplayColumn {
  @Prop({ required: true, index: true })
  relationId: string;

  @Prop({ required: true })
  columnName: string;
}

export const RelationDisplayColumnSchema = SchemaFactory.createForClass(RelationDisplayColumn);
