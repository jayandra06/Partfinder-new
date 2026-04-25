import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type WorksheetRelationDocument = HydratedDocument<WorksheetRelation>;

@Schema({ timestamps: true, collection: 'worksheet_relations' })
export class WorksheetRelation {
  @Prop({ required: true, index: true })
  orgId: string;

  @Prop({ required: true, index: true })
  primaryTemplateId: string;

  @Prop({ required: true, index: true })
  lookupTemplateId: string;

  @Prop({ required: true })
  name: string;

  @Prop({ required: true })
  triggerColumn: string;

  @Prop({ required: true })
  menuLabel: string;
}

export const WorksheetRelationSchema = SchemaFactory.createForClass(WorksheetRelation);
WorksheetRelationSchema.index({ orgId: 1, primaryTemplateId: 1 });
