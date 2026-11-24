import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { MongooseModule } from '@nestjs/mongoose';
import { DbClustersModule } from '../db-clusters/db-clusters.module';
import { OrganizationsModule } from '../organizations/organizations.module';
import { SetupController } from './setup.controller';
import { SetupService } from './setup.service';
import { SetupRefreshSessionService } from './setup-refresh-session.service';
import {
  SetupRefreshSession,
  SetupRefreshSessionSchema,
} from './schemas/setup-refresh-session.schema';
import { TenantMongoService } from './tenant-mongo.service';

import { CasbinModule } from '../casbin/casbin.module';
import { AuthModule } from '../auth/auth.module';

@Module({
  imports: [
    ConfigModule,
    OrganizationsModule,
    DbClustersModule,
    CasbinModule,
    AuthModule,
    MongooseModule.forFeature([
      { name: SetupRefreshSession.name, schema: SetupRefreshSessionSchema },
    ]),
  ],
  controllers: [SetupController],
  providers: [SetupService, SetupRefreshSessionService, TenantMongoService],
  exports: [SetupService, TenantMongoService, SetupRefreshSessionService],
})
export class SetupModule {}
