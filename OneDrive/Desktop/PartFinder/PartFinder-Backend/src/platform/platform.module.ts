import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { AuthModule } from '../auth/auth.module';
import { AdminPlatformController } from './admin-platform.controller';
import { PlatformSettingsService } from './platform-settings.service';
import { PublicPlatformController } from './public-platform.controller';
import {
  PlatformSettings,
  PlatformSettingsSchema,
} from './schemas/platform-settings.schema';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: PlatformSettings.name, schema: PlatformSettingsSchema },
    ]),
    AuthModule,
  ],
  controllers: [PublicPlatformController, AdminPlatformController],
  providers: [PlatformSettingsService],
  exports: [PlatformSettingsService],
})
export class PlatformModule {}
