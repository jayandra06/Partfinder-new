import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type OrganizationDocument = HydratedDocument<Organization>;

@Schema({ timestamps: true, collection: 'organizations' })
export class Organization {
  @Prop({ required: true, unique: true })
  orgCode: string;

  @Prop({ required: true })
  name: string;

  @Prop({ required: true })
  type: string;

  @Prop({ required: true })
  plan: string;

  @Prop({ required: true, type: Date })
  validity: Date;

  @Prop({ default: 'Active' })
  status: string;
}

export const OrganizationSchema = SchemaFactory.createForClass(Organization);