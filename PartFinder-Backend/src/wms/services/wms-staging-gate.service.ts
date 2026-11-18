import { ForbiddenException, Injectable } from '@nestjs/common';
import type { WmsRequestContext } from '../wms.types';
import { getWmsDeploymentTier } from '../wms-deployment-tier.util';
import { isWmsLiveStockPostingEnabled } from '../wms-live-stock.util';
import {
  isWmsStagingStockPostingEnabled,
  isWmsStockPostingAllowed,
} from '../wms-staging-stock.util';
import { WmsDiagnosticsService } from './wms-diagnostics.service';
import { WmsTenantCollectionsService } from './wms-tenant-collections.service';

/** Mandatory gate before Phase 03A stock mutations (receiving/put-away). */
@Injectable()
export class WmsStagingGateService {
  constructor(
    private readonly diagnostics: WmsDiagnosticsService,
    private readonly collections: WmsTenantCollectionsService,
  ) {}

  async recordPreflightEvidence(ctx: WmsRequestContext, tenantLabel?: string) {
    await this.collections.ensurePhase03AIndexes(ctx.tenantUri);
    const readiness = await this.diagnostics.probeTransactionReadiness(ctx.tenantUri);
    const coll = await this.collections.stagingGateEvidence(ctx.tenantUri);
    const now = new Date();
    const doc = {
      orgCode: ctx.orgCode,
      organizationName: ctx.organizationName,
      tenantDatabase: readiness.databaseName,
      replicaSetName: readiness.replicaSetName,
      commitProbe: readiness.commitProbe,
      rollbackProbe: readiness.rollbackProbe,
      supported: readiness.supported,
      detail: readiness.summary,
      wmsEnabled: ctx.wmsEnabled,
      deploymentTier: getWmsDeploymentTier(),
      stagingStockPosting: isWmsStagingStockPostingEnabled(),
      liveStockPosting: isWmsLiveStockPostingEnabled(),
      postingGateOpen: isWmsStockPostingAllowed() && readiness.supported,
      recordedBy: ctx.email,
      recordedAt: now,
      note: tenantLabel?.trim()
        ? `Operator note: ${tenantLabel.trim()}`
        : 'Derived from authenticated tenant session (orgCode).',
    };
    await coll.insertOne(doc);
    return doc;
  }

  async latestEvidence(ctx: WmsRequestContext) {
    const coll = await this.collections.stagingGateEvidence(ctx.tenantUri);
    return coll.findOne({}, { sort: { recordedAt: -1 } });
  }

  async assertReceivingStockMutationsAllowed(ctx: WmsRequestContext): Promise<void> {
    if (!isWmsStockPostingAllowed()) {
      throw new ForbiddenException(
        'Receiving stock mutations are disabled. WMS_STAGING_STOCK_POSTING must be true on staging only.',
      );
    }
    const evidence = await this.latestEvidence(ctx);
    if (!evidence) {
      throw new ForbiddenException(
        'MongoDB transaction preflight evidence required. POST /api/wms/v1/staging-gate/preflight on named staging tenant.',
      );
    }
    if ((evidence as { supported?: boolean }).supported !== true) {
      throw new ForbiddenException(
        'Latest preflight shows transactions unsupported for this tenant.',
      );
    }
  }
}