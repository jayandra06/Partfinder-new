import 'reflect-metadata';
import { ConfigService } from '@nestjs/config';
import { LlmGatewayService } from './llm-gateway.service';

describe('LlmGatewayService (Phase 6A)', () => {
  const originalFetch = global.fetch;

  afterEach(() => {
    global.fetch = originalFetch;
    jest.restoreAllMocks();
  });

  function createService(env: Record<string, string | undefined>) {
    const config = {
      get: jest.fn((key: string) => env[key]),
    } as unknown as ConfigService;
    return new LlmGatewayService(config);
  }

  it('reports unconfigured when LLM_INFERENCE_BASE_URL or LLM_MODEL is missing', async () => {
    const gateway = createService({ LLM_INFERENCE_BASE_URL: 'http://localhost:11434' });
    const res = await gateway.completeChat([{ role: 'user', content: 'hi' }]);

    expect(res.ok).toBe(false);
    expect(res.configured).toBe(false);
    expect(res.model).toBeNull();
  });

  it('rejects unsupported LLM_PROVIDER and invalid base URL', () => {
    const badProvider = createService({
      LLM_PROVIDER: 'openai_cloud',
      LLM_INFERENCE_BASE_URL: 'http://localhost:8000',
      LLM_MODEL: 'llama3',
    });
    expect(badProvider.getInferenceConfig().configured).toBe(false);
    expect(badProvider.getInferenceConfig().configError).toContain('Unsupported LLM_PROVIDER');

    const badUrl = createService({
      LLM_PROVIDER: 'vllm',
      LLM_INFERENCE_BASE_URL: 'ftp://localhost:8000',
      LLM_MODEL: 'llama3',
    });
    expect(badUrl.getInferenceConfig().configured).toBe(false);
    expect(badUrl.getInferenceConfig().configError).toContain('http or https');
  });

  it('handles normal and tool-call responses for openai_compatible and vllm', async () => {
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        model: 'qwen2.5-14b-instruct',
        choices: [
          {
            finish_reason: 'tool_calls',
            message: {
              content: '',
              tool_calls: [
                {
                  id: 'tc_1',
                  type: 'function',
                  function: {
                    name: 'get_template_schema',
                    arguments: '{"templateId":"tpl-1"}',
                  },
                },
              ],
            },
          },
        ],
        usage: { prompt_tokens: 50, completion_tokens: 15, total_tokens: 65 },
      }),
    });
    global.fetch = fetchMock as unknown as typeof fetch;

    const gateway = createService({
      LLM_PROVIDER: 'vllm',
      LLM_INFERENCE_BASE_URL: 'http://vllm.internal:8000/',
      LLM_MODEL: 'qwen2.5-14b-instruct',
      LLM_INFERENCE_SECRET: 'top-secret-token',
      LLM_MAX_TOKENS: '512',
    });

    const res = await gateway.completeChat(
      [{ role: 'user', content: 'Inspect template tpl-1' }],
      {
        tools: [
          {
            name: 'get_template_schema',
            description: 'Get schema',
            parameters: { type: 'object', properties: {} },
          },
        ],
      },
    );

    expect(res.ok).toBe(true);
    expect(res.provider).toBe('vllm');
    expect(res.toolCalls).toEqual([
      {
        id: 'tc_1',
        name: 'get_template_schema',
        arguments: { templateId: 'tpl-1' },
      },
    ]);
    expect(res.usage?.totalTokens).toBe(65);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe('http://vllm.internal:8000/v1/chat/completions');
    expect((init.headers as Record<string, string>).Authorization).toBe(
      'Bearer top-secret-token',
    );
    const body = JSON.parse(String(init.body));
    expect(body.max_tokens).toBe(512);
    expect(body.tool_choice).toBe('auto');
  });

  it('handles normal and tool-call responses for ollama', async () => {
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        model: 'llama3.1:8b',
        done_reason: 'stop',
        message: {
          role: 'assistant',
          content: '',
          tool_calls: [
            {
              function: {
                name: 'get_part_details',
                arguments: { search: 'M8' },
              },
            },
          ],
        },
        prompt_eval_count: 42,
        eval_count: 11,
      }),
    });
    global.fetch = fetchMock as unknown as typeof fetch;

    const gateway = createService({
      LLM_PROVIDER: 'ollama',
      LLM_INFERENCE_BASE_URL: 'http://127.0.0.1:11434',
      LLM_MODEL: 'llama3.1:8b',
    });

    const res = await gateway.completeChat(
      [{ role: 'user', content: 'Find M8 parts' }],
      {
        tools: [
          {
            name: 'get_part_details',
            description: 'Get part details',
            parameters: { type: 'object', properties: {} },
          },
        ],
      },
    );

    expect(res.ok).toBe(true);
    expect(res.provider).toBe('ollama');
    expect(res.toolCalls).toEqual([
      {
        id: 'call_1',
        name: 'get_part_details',
        arguments: { search: 'M8' },
      },
    ]);
    const [url] = fetchMock.mock.calls[0] as [string];
    expect(url).toBe('http://127.0.0.1:11434/api/chat');
  });

  it('rejects malformed tool-call arguments and malformed responses without executing text', async () => {
    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        choices: [
          {
            message: {
              content: '',
              tool_calls: [
                {
                  id: 'tc_bad',
                  function: {
                    name: 'get_part_details',
                    arguments: '{not-valid-json',
                  },
                },
              ],
            },
          },
        ],
      }),
    }) as unknown as typeof fetch;

    const gateway = createService({
      LLM_PROVIDER: 'openai_compatible',
      LLM_INFERENCE_BASE_URL: 'http://localhost:8000',
      LLM_MODEL: 'local-model',
    });

    const res = await gateway.completeChat([{ role: 'user', content: 'hi' }]);
    expect(res.ok).toBe(false);
    expect(res.toolCalls).toEqual([]);
    expect(res.error).toContain('Invalid JSON in tool arguments');

    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ unexpected: true }),
    }) as unknown as typeof fetch;
    const malformed = await gateway.completeChat([{ role: 'user', content: 'hi' }]);
    expect(malformed.ok).toBe(false);
    expect(malformed.error).toContain('missing choices');
  });

  it('handles HTTP errors, timeouts, and caller AbortSignal cancellation without leaking secrets', async () => {
    const gateway = createService({
      LLM_PROVIDER: 'openai_compatible',
      LLM_INFERENCE_BASE_URL: 'http://localhost:8000',
      LLM_MODEL: 'local-model',
      LLM_INFERENCE_SECRET: 'super-secret-value',
    });

    global.fetch = jest.fn().mockResolvedValue({ ok: false, status: 502 }) as unknown as typeof fetch;
    const httpErr = await gateway.completeChat([{ role: 'user', content: 'hi' }]);
    expect(httpErr.ok).toBe(false);
    expect(httpErr.error).toContain('502');
    expect(httpErr.error).not.toContain('super-secret-value');

    const timeoutErr = new Error('Timed out');
    timeoutErr.name = 'TimeoutError';
    global.fetch = jest.fn().mockRejectedValue(timeoutErr) as unknown as typeof fetch;
    const timedOut = await gateway.completeChat([{ role: 'user', content: 'hi' }]);
    expect(timedOut.ok).toBe(false);
    expect(timedOut.timedOut).toBe(true);

    const controller = new AbortController();
    controller.abort();
    const cancelled = await gateway.completeChat(
      [{ role: 'user', content: 'hi' }],
      { cancellationSignal: controller.signal },
    );
    expect(cancelled.ok).toBe(false);
    expect(cancelled.cancelled).toBe(true);
  });
});
