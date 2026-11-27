import { randomUUID } from 'crypto';
import {
  BadRequestException,
  Body,
  Controller,
  Delete,
  ForbiddenException,
  Get,
  Headers,
  Param,
  Post,
  Req,
  UnauthorizedException,
  UseGuards,
} from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import type { Request } from 'express';
import { CasbinService } from '../casbin/casbin.service';
import { OrganizationsService } from '../organizations/organizations.service';
import { AiOrchestratorService } from './ai-orchestrator.service';
import { ConversationIdentity, ConversationService } from './conversation.service';
import { AiChatRequestDto } from './dto/ai-chat.dto';

interface AuthenticatedUser {
  userId?: string;
  email?: string;
  orgCode?: string;
}

@Controller('ai')
@UseGuards(AuthGuard('jwt'))
export class AiController {
  constructor(
    private readonly orchestrator: AiOrchestratorService,
    private readonly conversations: ConversationService,
    private readonly organizations: OrganizationsService,
    private readonly casbin: CasbinService,
  ) {}

  @Post('chat')
  async chat(
    @Req() req: Request,
    @Headers('x-org-id') headerOrgId: string | undefined,
    @Body() dto: AiChatRequestDto,
  ) {
    const identity = await this.resolveAndAuthorize(req, headerOrgId);
    const requestIdHeader = req.headers['x-request-id'];
    const requestId =
      typeof requestIdHeader === 'string' && requestIdHeader.trim()
        ? requestIdHeader.trim()
        : randomUUID();
    const abortController = new AbortController();
    const onAborted = () => abortController.abort();
    req.on('aborted', onAborted);

    try {
      const data = await this.orchestrator.handleChat(identity, dto, {
        role: identity.role,
        requestId,
        cancellationSignal: abortController.signal,
      });
      return {
        data,
        success: true,
        message: 'Chat response generated',
      };
    } finally {
      req.off('aborted', onAborted);
    }
  }

  @Get('conversations')
  async listConversations(
    @Req() req: Request,
    @Headers('x-org-id') headerOrgId: string | undefined,
  ) {
    const identity = await this.resolveAndAuthorize(req, headerOrgId);
    const data = await this.conversations.listForUser(identity);
    return {
      data,
      success: true,
      message: 'Conversations fetched',
    };
  }

  @Get('conversations/:id')
  async getConversation(
    @Req() req: Request,
    @Headers('x-org-id') headerOrgId: string | undefined,
    @Param('id') id: string,
  ) {
    const identity = await this.resolveAndAuthorize(req, headerOrgId);
    const data = await this.conversations.getOwnedConversationLean(identity, id);
    return {
      data,
      success: true,
      message: 'Conversation fetched',
    };
  }

  @Delete('conversations/:id')
  async deleteConversation(
    @Req() req: Request,
    @Headers('x-org-id') headerOrgId: string | undefined,
    @Param('id') id: string,
  ) {
    const identity = await this.resolveAndAuthorize(req, headerOrgId);
    await this.conversations.deleteOwnedConversation(identity, id);
    return {
      data: { deleted: true, conversationId: id },
      success: true,
      message: 'Conversation deleted',
    };
  }

  private async resolveAndAuthorize(
    req: Request,
    headerOrgId?: string,
  ): Promise<ConversationIdentity> {
    const user = (req as Request & { user?: AuthenticatedUser }).user;
    const userId = user?.userId?.trim();
    const userEmail = user?.email?.trim().toLowerCase();
    if (!userId || !userEmail) {
      throw new UnauthorizedException('Authenticated user context is missing.');
    }

    const tokenOrg = user?.orgCode?.trim();
    const headerOrg = headerOrgId?.trim();
    if (tokenOrg && headerOrg && tokenOrg !== headerOrg) {
      throw new ForbiddenException('Organization header does not match token.');
    }

    const orgId = tokenOrg || headerOrg;
    if (!orgId) {
      throw new BadRequestException('Organization context is required.');
    }

    const org = await this.organizations.findByOrgCode(orgId);
    if (!org) {
      throw new ForbiddenException('Organization not found.');
    }

    if (org.licensePermanentlyBanned) {
      throw new ForbiddenException('Organization is disabled.');
    }

    if (
      org.licenseBannedUntil &&
      new Date(org.licenseBannedUntil).getTime() > Date.now()
    ) {
      throw new ForbiddenException('Organization is temporarily suspended.');
    }

    const tenantUri = org.orgDatabaseUri?.trim();

    let resolvedRole = '';

    if (tenantUri) {
      const enforcer = await this.casbin.getEnforcer(tenantUri);
      const roles = await enforcer.getRolesForUser(userEmail);
      const isAdmin = roles.some((r) => r.toLowerCase() === 'admin');
      resolvedRole = isAdmin ? 'admin' : roles[0] ?? '';
      if (!isAdmin) {
        const hasExplicitPolicy =
          (await enforcer.getFilteredPolicy(0, userEmail)).length > 0 ||
          roles.length > 0;
        if (hasExplicitPolicy) {
          const allowed =
            (await enforcer.enforce(userEmail, 'ai', 'use')) ||
            (await enforcer.enforce(userEmail, 'templates', 'view')) ||
            (await enforcer.enforce(userEmail, 'masterdata', 'view'));
          if (!allowed) {
            throw new ForbiddenException(
              'You do not have permission to use the AI assistant.',
            );
          }
        }
      }
    }

    return { orgId, userId, userEmail, role: resolvedRole };
  }
}
