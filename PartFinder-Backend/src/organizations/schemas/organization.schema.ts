import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Schema as MongooseSchema } from 'mongoose';

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

  /** When true, license verification always fails until cleared via reactivate. */
  @Prop({ default: false })
  licensePermanentlyBanned: boolean;

  /** When set and in the future, license fails until this instant (UTC). Ignored if permanently banned. */
  @Prop({ type: Date, default: null })
  licenseBannedUntil: Date | null;

  /** Per-organization MongoDB connection string (tenant data). Set only after tenant DB init succeeds. */
  @Prop({ type: String, default: null })
  orgDatabaseUri: string | null;

  /** Set when tenant DB was provisioned via “Default” mode (admin DBClusters pool). */
  @Prop({ type: MongooseSchema.Types.ObjectId, ref: 'DbCluster', default: null })
  assignedDbClusterId: MongooseSchema.Types.ObjectId | null;

  @Prop({ default: 50 })
  maxUsers: number;

  @Prop({ default: 100000 })
  maxParts: number;
}

export const OrganizationSchema = SchemaFactory.createForClass(Organization);