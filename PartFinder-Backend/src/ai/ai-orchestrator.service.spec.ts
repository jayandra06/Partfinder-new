import 'reflect-metadata';
import { Types } from 'mongoose';
import { AiOrchestratorService } from './ai-orchestrator.service';
import { ConversationIdentity, ConversationService } from './conversation.service';
import { LlmGatewayResult, LlmGatewayService } from './llm-gateway.service';
import { AiToolRegistryService } from './tools/ai-tool-registry.service';
import { AiToolDefinition, AiToolExecutionResult } from './tools/ai-tool.types';

describe('AiOrchestratorService (Phase 6B Tool-Calling Loop)', () => {
  const identity: ConversationIdentity = {
    orgId: '884470',
    userId: 'user-1',
    userEmail: 'engineer@partfinder.test',
    role: 'e',
  };

  function createHarness(options?: {
    gatewayImpl?: jest.Mock;
    executeToolImpl?: jest.Mock;
    definitions?: AiToolDefinition[];
  }) {
    const convId = new Types.ObjectId().toHexString();
    const fakeConversation: any = {
      _id: convId,
      orgId: identity.orgId,
      userId: identity.userId,
      userEmail: identity.userEmail,
      messages: [],
    };

    const conversations = {
      createOrLoad: jest.fn().mockResolvedValue(fakeConversation),
      appendMessages: jest.fn().mockImplementation(async (_id, doc, entries) => {
        doc.messages.push(...entries);
        return doc;
      }),
    } as unknown as jest.Mocked<Pick<ConversationService, 'createOrLoad' | 'appendMessages'>>;

    const gateway = {
      completeChat: options?.gatewayImpl ?? jest.fn(),
    } as unknown as jest.Mocked<Pick<LlmGatewayService, 'completeChat'>>;

    const defaultDefs: AiToolDefinition[] = options?.definitions ?? [
      {
        name: 'get_template_schema',
        description: 'Get schema',
        requiredPermission: { obj: 'templates', act: 'view' },
        readOnly: true,
        requiresConfirmation: false,
        maxResultLimit: 50,
        parametersSchema: { type: 'object', properties: {} },
      },
      {
        name: 'search_template_rows',
        description: 'Search rows',
        requiredPermission: { obj: 'templates', act: 'view' },
        readOnly: true,
        requiresConfirmation: false,
        maxResultLimit: 50,
        parametersSchema: { type: 'object', properties: {} },
      },
      {
        name: 'export_template_data',
        description: 'Export template',
        requiredPermission: { obj: 'templates', act: 'view' },
        readOnly: true,
        requiresConfirmation: false,
        maxResultLimit: 500,
        parametersSchema: { type: 'object', properties: {} },
      },
    ];

    const toolRegistry = {
      getToolDefinitions: jest.fn().mockReturnValue(defaultDefs),
      executeTool: options?.executeToolImpl ?? jest.fn(),
    } as unknown as jest.Mocked<Pick<AiToolRegistryService, 'getToolDefinitions' | 'executeTool'>>;

    const orchestrator = new AiOrchestratorService(
      conversations as unknown as ConversationService,
      gateway as unknown as LlmGatewayService,
      toolRegistry as unknown as AiToolRegistryService,
    );

    return { orchestrator, conversations, gateway, toolRegistry, fakeConversation };
  }

  function okResult(partial: Partial<LlmGatewayResult>): LlmGatewayResult {
    return {
      ok: true,
      configured: true,
      provider: 'vllm',
      content: '',
      model: 'qwen2.5',
      toolCalls: [],
      ...partial,
    };
  }

  it('1. handles normal LLM response without tool calls', async () => {
    const { orchestrator, gateway, toolRegistry, conversations } = createHarness({
      gatewayImpl: jest.fn().mockResolvedValue(
        okResult({ content: 'PartFinder manages templates and WMS stock.' }),
      ),
    });

    const res = await orchestrator.handleChat(identity, { message: 'What is PartFinder?' });

    expect(res.metadata.status).toBe('ok');
    expect(res.replyText).toBe('PartFinder manages templates and WMS stock.');
    expect(toolRegistry.executeTool).not.toHaveBeenCalled();
    expect(gateway.completeChat).toHaveBeenCalledTimes(1);
    expect(conversations.appendMessages).toHaveBeenCalledTimes(1);
  });

  it('2. executes a single tool call and returns final assistant response', async () => {
    const gatewayImpl = jest
      .fn()
      .mockResolvedValueOnce(
        okResult({
          toolCalls: [{ id: 'c1', name: 'get_template_schema', arguments: { templateId: 'tpl-1' } }],
        }),
      )
      .mockResolvedValueOnce(okResult({ content: 'Template Fasteners has 3 columns.' }));

    const executeToolImpl = jest.fn().mockResolvedValue({
      ok: true,
      tool: 'get_template_schema',
      dataSource: 'central_mongodb',
      returnedCount: 1,
      data: { template: { id: 'tpl-1', name: 'Fasteners' } },
    } satisfies AiToolExecutionResult);

    const { orchestrator, toolRegistry } = createHarness({ gatewayImpl, executeToolImpl });

    const res = await orchestrator.handleChat(identity, { message: 'Describe Fasteners' });

    expect(res.metadata.status).toBe('ok');
    expect(res.metadata.toolIterations).toBe(1);
    expect(res.replyText).toBe('Template Fasteners has 3 columns.');
    expect(toolRegistry.executeTool).toHaveBeenCalledWith(
      'get_template_schema',
      { templateId: 'tpl-1' },
      expect.objectContaining({ orgId: '884470', userId: 'user-1', email: 'engineer@partfinder.test', role: 'e' }),
    );
  });

  it('3. executes sequential tool calls across multiple iterations', async () => {
    const gatewayImpl = jest
      .fn()
      .mockResolvedValueOnce(
        okResult({ toolCalls: [{ id: 'c1', name: 'get_template_schema', arguments: {} }] }),
      )
      .mockResolvedValueOnce(
        okResult({
          toolCalls: [{ id: 'c2', name: 'search_template_rows', arguments: { templateId: 'tpl-1', query: 'M8' } }],
        }),
      )
      .mockResolvedValueOnce(okResult({ content: 'Found 2 M8 bolts.' }));

    const executeToolImpl = jest.fn().mockResolvedValue({
      ok: true,
      tool: 'search_template_rows',
      dataSource: 'central_mongodb',
      returnedCount: 2,
      data: { rows: [{ rowId: 'r1' }, { rowId: 'r2' }] },
    });

    const { orchestrator } = createHarness({ gatewayImpl, executeToolImpl });
    const res = await orchestrator.handleChat(identity, { message: 'Find M8 bolts' });

    expect(res.metadata.status).toBe('ok');
    expect(res.metadata.toolIterations).toBe(2);
    expect(executeToolImpl).toHaveBeenCalledTimes(2);
  });

  it('4. executes multiple tool calls in a single iteration', async () => {
    const gatewayImpl = jest
      .fn()
      .mockResolvedValueOnce(
        okResult({
          toolCalls: [
            { id: 'c1', name: 'get_template_schema', arguments: { templateId: 'tpl-1' } },
            { id: 'c2', name: 'search_template_rows', arguments: { templateId: 'tpl-1', query: 'SKF' } },
          ],
        }),
      )
      .mockResolvedValueOnce(okResult({ content: 'SKF bearings listed.' }));

    const executeToolImpl = jest.fn().mockResolvedValue({
      ok: true,
      tool: 'ok',
      dataSource: 'central_mongodb',
      returnedCount: 1,
      data: {},
    });

    const { orchestrator } = createHarness({ gatewayImpl, executeToolImpl });
    const res = await orchestrator.handleChat(identity, { message: 'Show SKF in tpl-1' });

    expect(res.metadata.status).toBe('ok');
    expect(executeToolImpl).toHaveBeenCalledTimes(2);
  });

  it('5-8. forwards invalid args, unknown tools, and unauthorized errors safely with verified tenant context', async () => {
    const gatewayImpl = jest
      .fn()
      .mockResolvedValueOnce(
        okResult({
          toolCalls: [
            { id: 'c1', name: 'unknown_db_drop', arguments: { orgId: 'evil-org' } },
            { id: 'c2', name: 'get_template_schema', arguments: { badArg: true } },
            { id: 'c3', name: 'get_part_details', arguments: { partId: 'p1' } },
          ],
        }),
      )
      .mockResolvedValueOnce(okResult({ content: 'Those actions are not permitted.' }));

    const executeToolImpl = jest
      .fn()
      .mockResolvedValueOnce({
        ok: false,
        tool: 'unknown_db_drop',
        dataSource: 'central_mongodb',
        error: { code: 'UNKNOWN_TOOL', message: 'Not registered' },
      })
      .mockResolvedValueOnce({
        ok: false,
        tool: 'get_template_schema',
        dataSource: 'central_mongodb',
        error: { code: 'INVALID_ARGUMENTS', message: 'badArg rejected' },
      })
      .mockResolvedValueOnce({
        ok: false,
        tool: 'get_part_details',
        dataSource: 'central_mongodb',
        error: { code: 'FORBIDDEN', message: 'Casbin denied' },
      });

    const { orchestrator } = createHarness({ gatewayImpl, executeToolImpl });
    const res = await orchestrator.handleChat(identity, { message: 'Run restricted actions' });

    expect(res.metadata.status).toBe('ok');
    for (const call of executeToolImpl.mock.calls) {
      expect(call[2].orgId).toBe('884470');
      expect(call[2].userId).toBe('user-1');
    }
  });

  it('9. prevents duplicate tool execution with canonicalized arguments', async () => {
    const gatewayImpl = jest
      .fn()
      .mockResolvedValueOnce(
        okResult({
          toolCalls: [
            { id: 'c1', name: 'search_template_rows', arguments: { templateId: 'tpl-1', query: 'M8' } },
            { id: 'c2', name: 'search_template_rows', arguments: { query: 'M8', templateId: 'tpl-1' } },
          ],
        }),
      )
      .mockResolvedValueOnce(okResult({ content: 'Done.' }));

    const executeToolImpl = jest.fn().mockResolvedValue({
      ok: true,
      tool: 'search_template_rows',
      dataSource: 'central_mongodb',
      returnedCount: 1,
      data: { rows: [] },
    });

    const { orchestrator } = createHarness({ gatewayImpl, executeToolImpl });
    await orchestrator.handleChat(identity, { message: 'Search M8' });

    expect(executeToolImpl).toHaveBeenCalledTimes(1);
  });

  it('10. enforces maximum 5 LLM iterations per request', async () => {
    let n = 0;
    const gatewayImpl = jest.fn().mockImplementation(async (_msgs, opts) => {
      if (opts?.tools?.length === 0) {
        return okResult({ content: 'Stopped at iteration limit.' });
      }
      n += 1;
      return okResult({
        toolCalls: [{ id: `c${n}`, name: 'get_template_schema', arguments: { templateId: `tpl-${n}` } }],
      });
    });

    const executeToolImpl = jest.fn().mockResolvedValue({
      ok: true,
      tool: 'get_template_schema',
      dataSource: 'central_mongodb',
      returnedCount: 1,
      data: {},
    });

    const { orchestrator } = createHarness({ gatewayImpl, executeToolImpl });
    const res = await orchestrator.handleChat(identity, { message: 'Loop forever' });

    expect(res.metadata.status).toBe('limit_reached');
    expect(res.metadata.toolIterations).toBe(5);
    expect(executeToolImpl).toHaveBeenCalledTimes(5);
    expect(res.replyText).toBe('Stopped at iteration limit.');
  });

  it('11-12. enforces max 4 tool calls per iteration and max 8 total executions across request', async () => {
    let callCounter = 0;
    const makeCalls = (count: number) =>
      Array.from({ length: count }, () => {
        callCounter += 1;
        return {
          id: `id_${callCounter}`,
          name: 'get_template_schema',
          arguments: { templateId: `tpl-${callCounter}` },
        };
      });

    const gatewayImpl = jest
      .fn()
      // Iter 1: asks for 5 calls -> only 4 executed, triggers limit_reached
      .mockResolvedValueOnce(okResult({ toolCalls: makeCalls(5) }))
      .mockResolvedValueOnce(okResult({ content: 'Capped at 4 per iteration.' }));

    const executeToolImpl = jest.fn().mockResolvedValue({
      ok: true,
      tool: 'get_template_schema',
      dataSource: 'central_mongodb',
      returnedCount: 1,
      data: {},
    });

    const harness1 = createHarness({ gatewayImpl, executeToolImpl });
    const res1 = await harness1.orchestrator.handleChat(identity, { message: '5 in 1 iter' });
    expect(res1.metadata.status).toBe('limit_reached');
    expect(executeToolImpl).toHaveBeenCalledTimes(4);

    // Total 8 across 3 iterations (4 + 4 + 1)
    callCounter = 0;
    const executeTool2 = jest.fn().mockResolvedValue({
      ok: true,
      tool: 'get_template_schema',
      dataSource: 'central_mongodb',
      returnedCount: 1,
      data: {},
    });
    const gateway2 = jest
      .fn()
      .mockResolvedValueOnce(okResult({ toolCalls: makeCalls(4) }))
      .mockResolvedValueOnce(okResult({ toolCalls: makeCalls(4) }))
      .mockResolvedValueOnce(okResult({ toolCalls: makeCalls(2) }))
      .mockResolvedValueOnce(okResult({ content: 'Stopped at 8 executions.' }));

    const harness2 = createHarness({ gatewayImpl: gateway2, executeToolImpl: executeTool2 });
    const res2 = await harness2.orchestrator.handleChat(identity, { message: '10 total' });
    expect(res2.metadata.status).toBe('limit_reached');
    expect(executeTool2).toHaveBeenCalledTimes(8);
  });

  it('13. handles timeout and AbortSignal cancellation without corrupting conversation', async () => {
    const timedOutHarness = createHarness({
      gatewayImpl: jest.fn().mockResolvedValue({
        ok: false,
        configured: true,
        provider: 'ollama',
        content: '',
        model: 'llama3.1',
        toolCalls: [],
        timedOut: true,
        error: 'Inference request timed out after 20000ms.',
      }),
    });
    const timeoutRes = await timedOutHarness.orchestrator.handleChat(identity, { message: 'Slow query' });
    expect(timeoutRes.metadata.status).toBe('timeout');
    expect(timedOutHarness.conversations.appendMessages).toHaveBeenCalledTimes(1);

    const ac = new AbortController();
    ac.abort();
    const cancelHarness = createHarness({
      gatewayImpl: jest.fn(),
    });
    const cancelRes = await cancelHarness.orchestrator.handleChat(
      identity,
      { message: 'Cancel me' },
      { cancellationSignal: ac.signal },
    );
    expect(cancelRes.metadata.status).toBe('cancelled');
    expect(cancelHarness.conversations.appendMessages).not.toHaveBeenCalled();
  });

  it('14-15. forwards full export payload to uiActions while bounding LLM tool content and persisting metadata', async () => {
    const bigRows = Array.from({ length: 120 }, (_, i) => ({ PartNumber: `P-${i}`, Price: `${i}` }));
    const exportPayload = {
      exportType: 'template_data',
      format: 'csv',
      fileNameSuggestion: 'template-fasteners.csv',
      columns: [{ key: 'PartNumber', label: 'PartNumber' }, { key: 'Price', label: 'Price' }],
      rows: bigRows,
      csvContent: 'PartNumber,Price\r\n' + bigRows.map((r) => `${r.PartNumber},${r.Price}`).join('\r\n'),
      rowCount: 120,
      truncated: false,
    };

    const gatewayImpl = jest
      .fn()
      .mockResolvedValueOnce(
        okResult({ toolCalls: [{ id: 'exp1', name: 'export_template_data', arguments: { templateId: 'tpl-1', format: 'csv' } }] }),
      )
      .mockResolvedValueOnce(
        okResult({
          toolCalls: [{ id: 'big1', name: 'search_template_rows', arguments: { templateId: 'tpl-1' } }],
        }),
      )
      .mockResolvedValueOnce(okResult({ content: 'Your export is ready.' }));

    const hugeData = { rows: Array.from({ length: 300 }, (_, i) => ({ rowId: `r-${i}`, note: 'X'.repeat(80) })) };
    const executeToolImpl = jest
      .fn()
      .mockResolvedValueOnce({
        ok: true,
        tool: 'export_template_data',
        dataSource: 'central_mongodb',
        returnedCount: 120,
        data: exportPayload,
      })
      .mockResolvedValueOnce({
        ok: true,
        tool: 'search_template_rows',
        dataSource: 'central_mongodb',
        returnedCount: 50,
        data: hugeData,
      });

    const { orchestrator, conversations } = createHarness({ gatewayImpl, executeToolImpl });
    const res = await orchestrator.handleChat(identity, { message: 'Export fasteners' });

    expect(res.metadata.status).toBe('ok');
    expect(res.uiActions).toEqual([{ type: 'export', exportPayload }]);
    // Verify second gateway call received compact export summary (no csvContent)
    const secondCallMsgs = gatewayImpl.mock.calls[1][0] as Array<{ role: string; content: string }>;
    const exportToolMsg = secondCallMsgs.find((m) => m.role === 'tool');
    expect(exportToolMsg?.content).not.toContain('P-119');
    // Verify third gateway call bounded oversized non-export result <= 12,000 chars
    const thirdCallMsgs = gatewayImpl.mock.calls[2][0] as Array<{ role: string; content: string }>;
    const toolMsgs = thirdCallMsgs.filter((m) => m.role === 'tool');
    expect(toolMsgs[1].content.length).toBeLessThanOrEqual(12_000);
    // Verify persistence includes sanitized uiActions and safe toolExecutions
    const persistedEntries = conversations.appendMessages.mock.calls[0][2];
    expect(persistedEntries[1].uiActions).toHaveLength(1);
    expect(persistedEntries[1].toolExecutions).toHaveLength(2);
  });

  it('16. handles provider errors and tool execution failures gracefully', async () => {
    const providerErrHarness = createHarness({
      gatewayImpl: jest.fn().mockResolvedValue({
        ok: false,
        configured: true,
        provider: 'vllm',
        content: '',
        model: 'qwen2.5',
        toolCalls: [],
        error: 'Inference server returned status 500.',
      }),
    });
    const res = await providerErrHarness.orchestrator.handleChat(identity, { message: 'Hello' });
    expect(res.metadata.status).toBe('inference_error');
    expect(res.replyText).toContain('500');
  });
});
