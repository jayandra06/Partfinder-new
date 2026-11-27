import {
  BadRequestException,
  Injectable,
  NotFoundException,
  Optional,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { RedisService } from '../common/redis/redis.service';
import {
  AiConversationContextConfig,
  resolveAiContextConfig,
} from '../config/backend-env';
import {
  AiConversation,
  AiConversationDocument,
  AiMessageSubdoc,
  AiToolExecutionMeta,
  AiUiAction,
} from './schemas/ai-conversation.schema';

const CACHE_TTL_SECONDS = 1800;

export interface ConversationIdentity {
  orgId: string;
  userId: string;
  userEmail: string;
  role?: string;
}

@Injectable()
export class ConversationService {
  constructor(
    @InjectModel(AiConversation.name)
    private readonly conversationModel: Model<AiConversationDocument>,
    private readonly redis: RedisService,
    @Optional() private readonly config?: ConfigService,
  ) {}

  getContextConfig(): AiConversationContextConfig {
    return resolveAiContextConfig((key) => this.config?.get<string>(key));
  }

  private cacheKey(orgId: string, userId: string, id: string): string {
    return `ai:conv:${orgId}:${userId}:${id}`;
  }

  async createOrLoad(
    identity: ConversationIdentity,
    conversationId?: string,
    firstMessagePreview?: string,
  ): Promise<AiConversationDocument> {
    if (conversationId) {
      return this.getOwnedConversation(identity, conversationId);
    }

    const title =
      (firstMessagePreview ?? 'New conversation').trim().slice(0, 60) ||
      'New conversation';

    return this.conversationModel.create({
      orgId: identity.orgId,
      userId: identity.userId,
      userEmail: identity.userEmail,
      title,
      messages: [],
      contextSummary: '',
      summarizedMessageCount: 0,
      summaryVersion: 0,
    });
  }

  async listForUser(identity: ConversationIdentity) {
    return this.conversationModel
      .find({ orgId: identity.orgId, userId: identity.userId })
      .select('_id title createdAt updatedAt')
      .sort({ updatedAt: -1 })
      .limit(50)
      .lean()
      .exec();
  }

  async getOwnedConversation(
    identity: ConversationIdentity,
    conversationId: string,
  ): Promise<AiConversationDocument> {
    if (!Types.ObjectId.isValid(conversationId)) {
      throw new BadRequestException('Invalid conversationId.');
    }

    const doc = await this.conversationModel
      .findOne({
        _id: conversationId,
        orgId: identity.orgId,
        userId: identity.userId,
      })
      .exec();

    if (!doc) {
      throw new NotFoundException('Conversation not found.');
    }

    return doc;
  }

  async getOwnedConversationLean(
    identity: ConversationIdentity,
    conversationId: string,
  ) {
    if (!Types.ObjectId.isValid(conversationId)) {
      throw new BadRequestException('Invalid conversationId.');
    }

    const cached = await this.redis.getJson<Record<string, unknown>>(
      this.cacheKey(identity.orgId, identity.userId, conversationId),
    );
    if (cached) {
      return cached;
    }

    const doc = await this.conversationModel
      .findOne({
        _id: conversationId,
        orgId: identity.orgId,
        userId: identity.userId,
      })
      .lean()
      .exec();

    if (!doc) {
      throw new NotFoundException('Conversation not found.');
    }

    await this.redis.setJson(
      this.cacheKey(identity.orgId, identity.userId, conversationId),
      doc,
      CACHE_TTL_SECONDS,
    );

    return doc;
  }

  async appendMessages(
    identity: ConversationIdentity,
    conversation: AiConversationDocument,
    entries: Array<{
      role: 'user' | 'assistant';
      content: string;
      uiActions?: AiUiAction[];
      toolExecutions?: AiToolExecutionMeta[];
    }>,
  ): Promise<AiConversationDocument> {
    const cfg = this.getContextConfig();
    const now = new Date();
    for (const entry of entries) {
      const msg: AiMessageSubdoc = {
        role: entry.role,
        content: entry.content,
        timestampUtc: now,
        uiActions: entry.uiActions?.length ? entry.uiActions : undefined,
        ...(entry.toolExecutions?.length
          ? { toolExecutions: entry.toolExecutions }
          : {}),
      };
      conversation.messages.push(msg);
    }

    if (conversation.messages.length > cfg.maxStoredMessages) {
      const trimmedCount =
        conversation.messages.length - cfg.maxStoredMessages;
      let sliced = conversation.messages.slice(-cfg.maxStoredMessages);
      // Never retain orphaned leading tool messages if tool roles are ever persisted
      while (
        sliced.length > 0 &&
        (sliced[0] as unknown as { role: string }).role === 'tool'
      ) {
        sliced = sliced.slice(1);
      }
      conversation.messages = sliced;
      if (typeof conversation.summarizedMessageCount === 'number') {
        conversation.summarizedMessageCount = Math.max(
          0,
          conversation.summarizedMessageCount - trimmedCount,
        );
      }
    }

    const saved = await conversation.save();
    await this.redis.delete(
      this.cacheKey(identity.orgId, identity.userId, String(saved._id)),
    );
    return saved;
  }

  async updateSummaryIfMatchesVersion(
    identity: ConversationIdentity,
    conversationId: string,
    expectedVersion: number,
    rawSummary: string,
    summarizedMessageCount: number,
  ): Promise<boolean> {
    if (!Types.ObjectId.isValid(conversationId)) {
      return false;
    }
    const cfg = this.getContextConfig();
    const safeSummary = this.redactSensitiveText(rawSummary).slice(
      0,
      cfg.maxSummaryChars,
    );

    const updated = await this.conversationModel
      .findOneAndUpdate(
        {
          _id: conversationId,
          orgId: identity.orgId,
          userId: identity.userId,
          $or: [
            { summaryVersion: expectedVersion },
            ...(expectedVersion === 0
              ? [{ summaryVersion: { $exists: false } }]
              : []),
          ],
        },
        {
          $set: {
            contextSummary: safeSummary,
            summarizedMessageCount,
          },
          $inc: { summaryVersion: 1 },
        },
        { new: true },
      )
      .exec();

    if (!updated) {
      return false;
    }

    await this.redis.delete(
      this.cacheKey(identity.orgId, identity.userId, conversationId),
    );
    return true;
  }

  selectContextWindow<
    T extends { role: string; content: string; toolCalls?: unknown[] },
  >(
    messages: T[],
    cfg: AiConversationContextConfig = this.getContextConfig(),
    budgetOverrideChars?: number,
  ): T[] {
    if (messages.length === 0) {
      return [];
    }

    const maxChars =
      budgetOverrideChars !== undefined
        ? Math.max(0, Math.floor(budgetOverrideChars))
        : cfg.maxPromptHistoryChars;
    if (maxChars <= 0) {
      return [];
    }

    const groups = this.groupAtomicMessageSequences(messages);
    const selectedGroups: T[][] = [];
    let totalMessages = 0;
    let totalChars = 0;

    for (let i = groups.length - 1; i >= 0; i--) {
      const group = groups[i];
      if (totalMessages + group.length > cfg.historyWindowMessages) {
        break;
      }

      const groupChars = this.measureGroupChars(group);
      if (totalChars + groupChars > maxChars) {
        if (
          selectedGroups.length === 0 &&
          group.length === 1 &&
          group[0].role !== 'tool' &&
          !(Array.isArray(group[0].toolCalls) && group[0].toolCalls.length > 0)
        ) {
          const overhead = 24;
          const remaining = maxChars - overhead;
          if (remaining >= 32) {
            const clipped = {
              ...group[0],
              content: `${(group[0].content ?? '').slice(0, remaining - 3)}...`,
            };
            selectedGroups.unshift([clipped]);
          }
        }
        break;
      }

      selectedGroups.unshift(group);
      totalMessages += group.length;
      totalChars += groupChars;
    }

    return selectedGroups.flat();
  }

  groupAtomicMessageSequences<
    T extends { role: string; content: string; toolCalls?: unknown[] },
  >(messages: T[]): T[][] {
    const groups: T[][] = [];
    let i = 0;

    while (i < messages.length) {
      const msg = messages[i];
      if (msg.role === 'tool') {
        // Skip orphaned tool results that lack a preceding assistant(toolCalls)
        i += 1;
        continue;
      }

      if (
        msg.role === 'assistant' &&
        Array.isArray(msg.toolCalls) &&
        msg.toolCalls.length > 0
      ) {
        const group: T[] = [msg];
        let j = i + 1;
        while (j < messages.length && messages[j].role === 'tool') {
          group.push(messages[j]);
          j += 1;
        }
        // Only retain complete sequences that have at least one tool response
        if (group.length > 1) {
          groups.push(group);
        }
        i = j;
        continue;
      }

      groups.push([msg]);
      i += 1;
    }

    return groups;
  }

  private measureGroupChars<
    T extends { role: string; content: string; toolCalls?: unknown[] },
  >(group: T[]): number {
    let total = 0;
    for (const m of group) {
      total += (m.content ?? '').length + 24;
      if (Array.isArray(m.toolCalls) && m.toolCalls.length > 0) {
        total += JSON.stringify(m.toolCalls).length;
      }
    }
    return total;
  }

  redactSensitiveText(input: string): string {
    if (!input) return '';
    return input
      .replace(/mongodb(?:\+srv)?:\/\/[^\s"'`]+/gi, '[REDACTED_DB_URI]')
      .replace(/Bearer\s+[A-Za-z0-9\-._~+/]+=*/gi, 'Bearer [REDACTED_TOKEN]')
      .replace(
        /\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\b/g,
        '[REDACTED_JWT]',
      )
      .replace(
        /\b(password|secret|api[_-]?key|access[_-]?token)\s*[:=]\s*[^\s,;]+/gi,
        '$1=[REDACTED]',
      );
  }

  async deleteOwnedConversation(
    identity: ConversationIdentity,
    conversationId: string,
  ): Promise<void> {
    if (!Types.ObjectId.isValid(conversationId)) {
      throw new BadRequestException('Invalid conversationId.');
    }

    const res = await this.conversationModel
      .deleteOne({
        _id: conversationId,
        orgId: identity.orgId,
        userId: identity.userId,
      })
      .exec();

    if (res.deletedCount === 0) {
      throw new NotFoundException('Conversation not found.');
    }

    await this.redis.delete(
      this.cacheKey(identity.orgId, identity.userId, conversationId),
    );
  }
}
