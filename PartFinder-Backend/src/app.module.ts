import { Module } from '@nestjs/common';
import { ConfigModule, ConfigService } from '@nestjs/config';
import { EventEmitterModule } from '@nestjs/event-emitter';
import { MongooseModule } from '@nestjs/mongoose';
import { AppController } from './app.controller';
import { backendEnvFilePaths } from './config/backend-env';
import { AppService } from './app.service';
import { AuthModule } from './auth/auth.module';
import { assertMongoUri, normalizeMongoUri } from './mongo-uri';
import { configureAtlasFriendlyDns } from './test-utils/mongodb-atlas-dns';
import { DbClustersModule } from './db-clusters/db-clusters.module';
import { DebugModule } from './debug/debug.module';
import { ImportModule } from './import/import.module';
import { LicenseModule } from './license/license.module';
import { LicensePurchaseModule } from './license-purchase/license-purchase.module';
import { RedisModule } from './common/redis/redis.module';
import { ElasticsearchModule } from './common/elasticsearch/elasticsearch.module';
import { OrganizationsModule } from './organizations/organizations.module';
import { PartsModule } from './parts/parts.module';
import { PlatformModule } from './platform/platform.module';
import { RelationsModule } from './relations/relations.module';
import { SetupModule } from './setup/setup.module';
import { TemplatesModule } from './templates/templates.module';
import { UsersModule } from './users/users.module';
import { ViewDataModule } from './view-data/view-data.module';
import { DashboardModule } from './dashboard/dashboard.module';
import { WmsModule } from './wms/wms.module';

@Module({
  imports: [
    EventEmitterModule.forRoot(),
    ConfigModule.forRoot({
      isGlobal: true,
      envFilePath: backendEnvFilePaths(),
    }),
    MongooseModule.forRootAsync({
      imports: [ConfigModule],
      useFactory: (config: ConfigService) => {
        const uri = normalizeMongoUri(config.get<string>('MONGODB_URI'));
        assertMongoUri(uri);
        if (uri.startsWith('mongodb+srv://')) {
          configureAtlasFriendlyDns();
        }
        return { uri };
      },
      inject: [ConfigService],
    }),
    RedisModule,
    ElasticsearchModule,
    UsersModule,
    AuthModule,
    OrganizationsModule,
    DbClustersModule,
    LicenseModule,
    LicensePurchaseModule,
    PlatformModule,
    TemplatesModule,
    RelationsModule,
    PartsModule,
    DashboardModule,
    ViewDataModule,
    ImportModule,
    SetupModule,
    WmsModule,
    DebugModule,
  ],
  controllers: [AppController],
  providers: [AppService],
})
export class AppModule {}
