import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { DbClustersModule } from '../db-clusters/db-clusters.module';
import { OrganizationsModule } from '../organizations/organizations.module';
import { SetupController } from './setup.controller';
import { SetupService } from './setup.service';
import { TenantMongoService } from './tenant-mongo.service';

@Module({
  imports: [ConfigModule, OrganizationsModule, DbClustersModule],
  controllers: [SetupController],
  providers: [SetupService, TenantMongoService],
  exports: [SetupService, TenantMongoService],
})
export class SetupModule {}
