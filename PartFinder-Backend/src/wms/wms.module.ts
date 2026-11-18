import { Module } from '@nestjs/common';
import { AuthModule } from '../auth/auth.module';
import { OrganizationsModule } from '../organizations/organizations.module';
import { SetupModule } from '../setup/setup.module';
import { WmsSetupController } from './wms-setup.controller';
import { WmsPhase02Controller } from './wms-phase02.controller';
import { WmsPhase03aController } from './wms-phase03a.controller';
import { WmsSessionGuard } from './guards/wms-session.guard';
import { WmsSetupService } from './services/wms-setup.service';
import { WmsTenantCollectionsService } from './services/wms-tenant-collections.service';
import { WmsDiagnosticsService } from './services/wms-diagnostics.service';
import { WmsSkuService } from './services/wms-sku.service';
import { WmsSkuAuditService } from './services/wms-sku-audit.service';
import { WmsUomService } from './services/wms-uom.service';
import { WmsLedgerDryRunService } from './services/wms-ledger-dry-run.service';
import { WmsErpFoundationService } from './services/wms-erp-foundation.service';
import { WmsInventoryPostingService } from './services/wms-inventory-posting.service';
import { WmsInventoryReadService } from './services/wms-inventory-read.service';
import { WmsTransactionGuardService } from './services/wms-transaction-guard.service';
import { WmsReceivingService } from './services/wms-receiving.service';
import { WmsStagingGateService } from './services/wms-staging-gate.service';
import { WmsOperatorGrantsService } from './services/wms-operator-grants.service';

@Module({
  imports: [AuthModule, OrganizationsModule, SetupModule],
  controllers: [WmsSetupController, WmsPhase02Controller, WmsPhase03aController],
  providers: [
    WmsSessionGuard,
    WmsSetupService,
    WmsTenantCollectionsService,
    WmsDiagnosticsService,
    WmsSkuService,
    WmsSkuAuditService,
    WmsUomService,
    WmsLedgerDryRunService,
    WmsErpFoundationService,
    WmsInventoryPostingService,
    WmsInventoryReadService,
    WmsTransactionGuardService,
    WmsReceivingService,
    WmsStagingGateService,
    WmsOperatorGrantsService,
  ],
})
export class WmsModule {}