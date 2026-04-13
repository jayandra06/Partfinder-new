import { Module } from '@nestjs/common';
import { OrganizationsModule } from '../organizations/organizations.module';
import { LicenseController } from './license.controller';

@Module({
  imports: [OrganizationsModule],
  controllers: [LicenseController],
})
export class LicenseModule {}