import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { AuthModule } from '../auth/auth.module';
import {
  Organization,
  OrganizationSchema,
} from '../organizations/schemas/organization.schema';
import { DbClustersController } from './db-clusters.controller';
import { DbClustersService } from './db-clusters.service';
import { DbCluster, DbClusterSchema } from './schemas/db-cluster.schema';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: DbCluster.name, schema: DbClusterSchema },
      { name: Organization.name, schema: OrganizationSchema },
    ]),
    AuthModule,
  ],
  controllers: [DbClustersController],
  providers: [DbClustersService],
  exports: [DbClustersService],
})
export class DbClustersModule {}
