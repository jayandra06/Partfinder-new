import { Injectable } from '@nestjs/common';
import { CasbinService } from '../../casbin/casbin.service';
import { OrganizationsService } from '../../organizations/organizations.service';
import {
  AiToolExecutionContext,
  AiToolPermissionRequirement,
} from './ai-tool.types';

export interface AiToolAuthResult {
  allowed: boolean;
  tenantDatabaseConfigured: boolean;
  reason?: string;
}

@Injectable()
export class AiToolAuthService {
  constructor(
    private readonly organizationsService: OrganizationsService,
    private readonly casbinService: CasbinService,
  ) {}

  async authorize(
    context: AiToolExecutionContext,
    requirement: AiToolPermissionRequirement,
  ): Promise<AiToolAuthResult> {
    const orgId = context.orgId?.trim();
    const userId = context.userId?.trim();
    if (!orgId || !userId) {
      return {
        allowed: false,
        tenantDatabaseConfigured: false,
        reason: 'Missing organization or user identity.',
      };
    }

    const org = await this.organizationsService.findByOrgCode(orgId);
    if (!org) {
      return {
        allowed: false,
        tenantDatabaseConfigured: false,
        reason: 'Organization not found.',
      };
    }

    const tenantUri = org.orgDatabaseUri?.trim() ?? '';
    const tenantDatabaseConfigured = tenantUri.length > 0;

    if (org.licensePermanentlyBanned) {
      return {
        allowed: false,
        tenantDatabaseConfigured,
        reason: 'Organization license is disabled.',
      };
    }

    if (
      org.licenseBannedUntil &&
      new Date(org.licenseBannedUntil).getTime() > Date.now()
    ) {
      return {
        allowed: false,
        tenantDatabaseConfigured,
        reason: 'Organization license is temporarily suspended.',
      };
    }

    if (!tenantDatabaseConfigured) {
      return {
        allowed: true,
        tenantDatabaseConfigured: false,
      };
    }

    const subject =
      context.role?.trim() || context.email?.trim().toLowerCase() || userId;
    let allowed = await this.casbinService.checkPermission(
      tenantUri,
      subject,
      requirement.obj,
      requirement.act,
    );

    if (!allowed && context.email?.trim()) {
      const emailSubject = context.email.trim().toLowerCase();
      if (emailSubject !== subject) {
        allowed = await this.casbinService.checkPermission(
          tenantUri,
          emailSubject,
          requirement.obj,
          requirement.act,
        );
      }
    }

    return {
      allowed,
      tenantDatabaseConfigured: true,
      reason: allowed
        ? undefined
        : `Permission denied for ${requirement.obj}:${requirement.act}.`,
    };
  }
}
