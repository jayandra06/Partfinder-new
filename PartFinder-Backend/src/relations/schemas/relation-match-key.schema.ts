import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type RelationMatchKeyDocument = HydratedDocument<RelationMatchKey>;

@Schema({ timestamps: true, collection: 'relation_match_keys' })
export class RelationMatchKey {
  @Prop({ required: true, index: true })
  relationId: string;

  @Prop({ required: true })
  sourceColumn: string;

  @Prop({ required: true })
  targetColumn: string;
}

export const RelationMatchKeySchema = SchemaFactory.createForClass(RelationMatchKey);
RelationMatchKeySchema.index({ relationId: 1 });
