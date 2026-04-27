import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type DbClusterDocument = HydratedDocument<DbCluster>;

@Schema({ timestamps: true, collection: 'DBClusters' })
export class DbCluster {
  @Prop({ required: true })
  connectionUri: string;

  /** Max tenant databases (orgs on “Default”) per cluster. */
  @Prop({ default: 250 })
  maxDatabases: number;
}

export const DbClusterSchema = SchemaFactory.createForClass(DbCluster);
