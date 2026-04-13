import { Module } from '@nestjs/common';
import { ConfigModule, ConfigService } from '@nestjs/config';
import { MongooseModule } from '@nestjs/mongoose';
import { join } from 'path';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { AuthModule } from './auth/auth.module';
import { assertMongoUri, normalizeMongoUri } from './mongo-uri';
import { DbClustersModule } from './db-clusters/db-clusters.module';
import { DebugModule } from './debug/debug.module';
import { LicenseModule } from './license/license.module';
import { OrganizationsModule } from './organizations/organizations.module';
import { SetupModule } from './setup/setup.module';
import { UsersModule } from './users/users.module';

@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
      envFilePath: [join(process.cwd(), '.env')],
    }),
    MongooseModule.forRootAsync({
      imports: [ConfigModule],
      useFactory: (config: ConfigService) => {
        const uri = normalizeMongoUri(config.get<string>('MONGODB_URI'));
        assertMongoUri(uri);
        return { uri };
      },
      inject: [ConfigService],
    }),
    UsersModule,
    AuthModule,
    OrganizationsModule,
    DbClustersModule,
    LicenseModule,
    SetupModule,
    DebugModule,
  ],
  controllers: [AppController],
  providers: [AppService],
})
export class AppModule {}
