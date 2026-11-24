import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { AuthModule } from '../auth/auth.module';
import {
  Organization,
  OrganizationSchema,
} from '../organizations/schemas/organization.schema';
import { AdminUser, AdminUserSchema } from '../users/schemas/admin-user.schema';
import { AdminPlatformController } from './admin-platform.controller';
import { PlatformPublicSummaryService } from './platform-public-summary.service';
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
      { name: Organization.name, schema: OrganizationSchema },
      { name: AdminUser.name, schema: AdminUserSchema },
    ]),
    AuthModule,
  ],
  controllers: [PublicPlatformController, AdminPlatformController],
  providers: [PlatformSettingsService, PlatformPublicSummaryService],
  exports: [PlatformSettingsService],
})
export class PlatformModule {}
