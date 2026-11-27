import 'reflect-metadata';
import {
  BadRequestException,
  ForbiddenException,
  NotFoundException,
  UnauthorizedException,
  ValidationPipe,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { Types } from 'mongoose';
import { AiController } from './ai.controller';
import { AiOrchestratorService } from './ai-orchestrator.service';
import { ConversationService } from './conversation.service';
import { AiChatRequestDto } from './dto/ai-chat.dto';
import { LlmGatewayService } from './llm-gateway.service';

describe('AiModule (Phase 3 Foundation)', () => {
  const originalFetch = global.fetch;

  afterEach(() => {
    global.fetch = originalFetch;
    jest.restoreAllMocks();
  });

  describe('DTO Validation', () => {
    const pipe = new ValidationPipe({
      whitelist: true,
      forbidNonWhitelisted: true,
      transform: true,
    });

    it('accepts valid chat request with optional appContext', async () => {
      const dto = (await pipe.transform(
        {
          message: 'Find bearings in Datenbank',
          appContext: { currentPage: 'MasterData', selectedTemplateId: 'tpl-1' },
        },
        { type: 'body', metatype: AiChatRequestDto },
      )) as AiChatRequestDto;

      expect(dto.message).toBe('Find bearings in Datenbank');
      expect(dto.appContext?.currentPage).toBe('MasterData');
    });

    it('rejects empty message and unknown properties', async () => {
      await expect(
        pipe.transform(
          { message: '' },
          { type: 'body', metatype: AiChatRequestDto },
        ),
      ).rejects.toThrow(BadRequestException);

      await expect(
        pipe.transform(
          { message: 'hi', unexpectedField: true },
          { type: 'body', metatype: AiChatRequestDto },
        ),
      ).rejects.toThrow(BadRequestException);
    });
  });

  describe('LlmGatewayService', () => {
    it('reports unconfigured when LLM_INFERENCE_BASE_URL or LLM_MODEL is missing', async () => {
      const config = {
        get: jest.fn().mockReturnValue(undefined),
      } as unknown as ConfigService;
      const gateway = new LlmGatewayService(config);

      const res = await gateway.completeChat([{ role: 'user', content: 'hi' }]);

      expect(res.configured).toBe(false);
      expect(res.ok).toBe(false);
      expect(res.model).toBeNull();
    });

    it('handles HTTP failures from inference server safely', async () => {
      const config = {
        get: jest.fn((key: string) => {
          if (key === 'LLM_INFERENCE_BASE_URL') return 'https://llm.internal';
          if (key === 'LLM_MODEL') return 'Qwen/Qwen2.5-14B-Instruct';
          if (key === 'LLM_INFERENCE_SECRET') return 'top-secret';
          return undefined;
        }),
      } as unknown as ConfigService;
      global.fetch = jest.fn().mockResolvedValue({
        ok: false,
        status: 503,
      } as Response);

      const gateway = new LlmGatewayService(config);
      const res = await gateway.completeChat([{ role: 'user', content: 'hi' }]);

      expect(res.configured).toBe(true);
      expect(res.ok).toBe(false);
      expect(res.error).toContain('503');
      expect(res.error).not.toContain('top-secret');
    });

    it('handles timeouts from inference server', async () => {
      const config = {
        get: jest.fn((key: string) => {
          if (key === 'LLM_INFERENCE_BASE_URL') return 'https://llm.internal';
          if (key === 'LLM_MODEL') return 'Qwen/Qwen2.5-14B-Instruct';
          if (key === 'LLM_TIMEOUT_MS') return '2000';
          return undefined;
        }),
      } as unknown as ConfigService;
      const timeoutErr = new Error('The operation was aborted due to timeout');
      timeoutErr.name = 'TimeoutError';
      global.fetch = jest.fn().mockRejectedValue(timeoutErr);

      const gateway = new LlmGatewayService(config);
      const res = await gateway.completeChat([{ role: 'user', content: 'hi' }]);

      expect(res.ok).toBe(false);
      expect(res.timedOut).toBe(true);
      expect(res.error).toContain('timed out');
    });
  });

  describe('ConversationService (isolation & caching)', () => {
    const identityA = { orgId: '884470', userId: 'u1', userEmail: 'a@org.com' };
    const identityB = { orgId: '884470', userId: 'u2', userEmail: 'b@org.com' };
    const validId = new Types.ObjectId().toHexString();

    it('scopes getOwnedConversationLean by orgId and userId and caches in Redis', async () => {
      const doc = { _id: validId, orgId: '884470', userId: 'u1', messages: [] };
      const exec = jest.fn().mockResolvedValueOnce(doc).mockResolvedValueOnce(null);
      const lean = jest.fn(() => ({ exec }));
      const findOne = jest.fn(() => ({ lean }));
      const model = { findOne } as any;
      const redis = {
        getJson: jest.fn().mockResolvedValue(null),
        setJson: jest.fn().mockResolvedValue(undefined),
        delete: jest.fn().mockResolvedValue(undefined),
      } as any;

      const svc = new ConversationService(model, redis);
      const result = await svc.getOwnedConversationLean(identityA, validId);

      expect(result).toEqual(doc);
      expect(findOne).toHaveBeenCalledWith({
        _id: validId,
        orgId: '884470',
        userId: 'u1',
      });
      expect(redis.setJson).toHaveBeenCalled();

      await expect(
        svc.getOwnedConversationLean(identityB, validId),
      ).rejects.toThrow(NotFoundException);
    });

    it('rejects malformed conversationId', async () => {
      const svc = new ConversationService({} as any, {} as any);
      await expect(
        svc.getOwnedConversationLean(identityA, 'not-an-object-id'),
      ).rejects.toThrow(BadRequestException);
    });
  });

  describe('AiOrchestratorService', () => {
    it('persists conversation and reports unconfigured when no LLM server is set', async () => {
      const id = new Types.ObjectId().toHexString();
      const fakeDoc: any = {
        _id: id,
        messages: [],
        save: jest.fn().mockImplementation(async function (this: any) {
          return this;
        }),
      };
      const conversations = {
        createOrLoad: jest.fn().mockResolvedValue(fakeDoc),
        appendMessages: jest.fn().mockImplementation(async (_id, doc, entries) => {
          doc.messages.push(...entries);
          return doc;
        }),
      } as unknown as ConversationService;

      const config = {
        get: jest.fn().mockReturnValue(undefined),
      } as unknown as ConfigService;
      const gateway = new LlmGatewayService(config);
      const orchestrator = new AiOrchestratorService(conversations, gateway);

      const res = await orchestrator.handleChat(
        { orgId: '884470', userId: 'u1', userEmail: 'a@org.com' },
        { message: 'Hello' },
      );

      expect(res.conversationId).toBe(id);
      expect(res.metadata.inferenceConfigured).toBe(false);
      expect(res.metadata.status).toBe('unconfigured');
      expect(res.uiActions).toEqual([]);
    });

    it('strips disallowed uiActions to prevent arbitrary navigation or execution', () => {
      const orchestrator = new AiOrchestratorService({} as any, {} as any);
      const sanitized = orchestrator.sanitizeUiActions([
        { type: 'navigate', page: 'MasterData', templateId: 'tpl-1' },
        { type: 'exec', command: 'rm -rf /' },
        { type: 'navigate', page: 'ArbitraryPage' },
      ]);

      expect(sanitized).toEqual([
        { type: 'navigate', page: 'MasterData', templateId: 'tpl-1' },
      ]);
    });
  });

  describe('AiController (Auth, Org Isolation, Casbin)', () => {
    it('rejects missing authenticated user and mismatched x-org-id', async () => {
      const controller = new AiController(
        {} as any,
        {} as any,
        { findByOrgCode: jest.fn() } as any,
        {} as any,
      );

      await expect(
        controller.listConversations({} as any, '884470'),
      ).rejects.toThrow(UnauthorizedException);

      await expect(
        controller.listConversations(
          { user: { userId: 'u1', email: 'a@org.com', orgCode: '884470' } } as any,
          '999999',
        ),
      ).rejects.toThrow(ForbiddenException);
    });

    it('enforces Casbin permissions when tenant has explicit policies', async () => {
      const enforcer = {
        getRolesForUser: jest.fn().mockResolvedValue(['employee']),
        getFilteredPolicy: jest
          .fn()
          .mockResolvedValue([['e@org.com', 'parts', 'view']]),
        enforce: jest.fn().mockResolvedValue(false),
      };
      const controller = new AiController(
        {} as any,
        {} as any,
        {
          findByOrgCode: jest.fn().mockResolvedValue({
            orgCode: '884470',
            licensePermanentlyBanned: false,
            licenseBannedUntil: null,
            orgDatabaseUri: 'mongodb://localhost:27017/tenant',
          }),
        } as any,
        { getEnforcer: jest.fn().mockResolvedValue(enforcer) } as any,
      );

      await expect(
        controller.chat(
          { user: { userId: 'u1', email: 'e@org.com', orgCode: '884470' } } as any,
          undefined,
          { message: 'Hello' },
        ),
      ).rejects.toThrow(ForbiddenException);
    });
  });
});
