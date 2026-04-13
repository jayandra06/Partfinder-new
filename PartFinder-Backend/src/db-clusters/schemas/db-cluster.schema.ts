import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument } from 'mongoose';

export type DbClusterDocument = HydratedDocument<DbCluster>;

@Schema({ timestamps: true, collection: 'DBClusters' })
export class DbCluster {
  @Prop({ required: true })
  connectionUri: string;
}

export const DbClusterSchema = SchemaFactory.createForClass(DbCluster);
