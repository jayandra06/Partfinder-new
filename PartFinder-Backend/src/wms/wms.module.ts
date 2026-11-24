import { Module } from '@nestjs/common';
import { AuthModule } from '../auth/auth.module';
import { OrganizationsModule } from '../organizations/organizations.module';
import { SetupModule } from '../setup/setup.module';
import { WmsSetupController } from './wms-setup.controller';
import { WmsSessionGuard } from './guards/wms-session.guard';
import { WmsSetupService } from './services/wms-setup.service';
import { WmsTenantCollectionsService } from './services/wms-tenant-collections.service';
import { WmsDiagnosticsService } from './services/wms-diagnostics.service';

@Module({
  imports: [AuthModule, OrganizationsModule, SetupModule],
  controllers: [WmsSetupController],
  providers: [
    WmsSessionGuard,
    WmsSetupService,
    WmsTenantCollectionsService,
    WmsDiagnosticsService,
  ],
})
export class WmsModule {}