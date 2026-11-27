import { Injectable, Logger } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import {
  LlmInferenceConfig,
  LlmProviderType,
  resolveLlmInferenceConfig,
} from '../config/backend-env';

export interface LlmToolCallRequest {
  id: string;
  name: string;
  arguments: Record<string, unknown>;
}

export interface LlmToolSchema {
  name: string;
  description: string;
  parameters: Record<string, unknown>;
}

export interface LlmChatMessage {
  role: 'system' | 'user' | 'assistant' | 'tool';
  content: string;
  toolCalls?: LlmToolCallRequest[];
  toolCallId?: string;
  name?: string;
}

export interface LlmCompletionOptions {
  tools?: LlmToolSchema[];
  maxTokens?: number;
  cancellationSignal?: AbortSignal;
}

export interface LlmGatewayResult {
  ok: boolean;
  configured: boolean;
  provider: LlmProviderType;
  content: string;
  model: string | null;
  toolCalls: LlmToolCallRequest[];
  finishReason?: string;
  usage?: {
    promptTokens?: number;
    completionTokens?: number;
    totalTokens?: number;
  };
  error?: string;
  timedOut?: boolean;
  cancelled?: boolean;
}

@Injectable()
export class LlmGatewayService {
  private readonly logger = new Logger(LlmGatewayService.name);

  constructor(private readonly config: ConfigService) {}

  getInferenceConfig(): LlmInferenceConfig {
    return resolveLlmInferenceConfig((key) => this.config.get<string>(key));
  }

  async completeChat(
    messages: LlmChatMessage[],
    optionsOrSignal?: LlmCompletionOptions | AbortSignal,
    legacySignal?: AbortSignal,
  ): Promise<LlmGatewayResult> {
    const options: LlmCompletionOptions =
      optionsOrSignal && 'aborted' in optionsOrSignal
        ? { cancellationSignal: optionsOrSignal }
        : {
            ...(optionsOrSignal ?? {}),
            cancellationSignal:
              optionsOrSignal?.cancellationSignal ?? legacySignal,
          };

    const cfg = this.getInferenceConfig();
    if (!cfg.configured) {
      return {
        ok: false,
        configured: false,
        provider: cfg.provider,
        content: '',
        model: null,
        toolCalls: [],
        error:
          cfg.configError ??
          'Self-hosted LLM inference server is not configured (set LLM_INFERENCE_BASE_URL and LLM_MODEL).',
      };
    }

    if (options.cancellationSignal?.aborted) {
      return {
        ok: false,
        configured: true,
        provider: cfg.provider,
        content: '',
        model: cfg.model,
        toolCalls: [],
        error: 'Inference request was cancelled.',
        cancelled: true,
      };
    }

    const maxTokens =
      options.maxTokens &&
      Number.isFinite(options.maxTokens) &&
      options.maxTokens >= 64
        ? Math.floor(options.maxTokens)
        : cfg.maxTokens;

    const timeoutSignal = AbortSignal.timeout(cfg.timeoutMs);
    const signal = options.cancellationSignal
      ? AbortSignal.any([timeoutSignal, options.cancellationSignal])
      : timeoutSignal;

    const { endpoint, payload } = this.buildProviderRequest(
      cfg,
      messages,
      options.tools,
      maxTokens,
    );

    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };
    if (cfg.secret) {
      headers.Authorization = `Bearer ${cfg.secret}`;
    }

    try {
      const response = await fetch(endpoint, {
        method: 'POST',
        headers,
        body: JSON.stringify(payload),
        signal,
      });

      if (!response.ok) {
        this.logger.warn(
          `LLM inference server (${cfg.provider}) returned HTTP ${response.status}`,
        );
        return {
          ok: false,
          configured: true,
          provider: cfg.provider,
          content: '',
          model: cfg.model,
          toolCalls: [],
          error: `Inference server returned status ${response.status}.`,
        };
      }

      const rawBody = (await response.json()) as unknown;
      return this.parseProviderResponse(cfg, rawBody);
    } catch (err: unknown) {
      if (options.cancellationSignal?.aborted) {
        this.logger.warn('Inference request cancelled by caller.');
        return {
          ok: false,
          configured: true,
          provider: cfg.provider,
          content: '',
          model: cfg.model,
          toolCalls: [],
          error: 'Inference request was cancelled.',
          cancelled: true,
        };
      }

      const isTimeout =
        err instanceof Error &&
        (err.name === 'TimeoutError' || err.name === 'AbortError');
      const message = isTimeout
        ? `Inference request timed out after ${cfg.timeoutMs}ms.`
        : 'Could not reach the self-hosted LLM inference server.';
      this.logger.warn(message);
      return {
        ok: false,
        configured: true,
        provider: cfg.provider,
        content: '',
        model: cfg.model,
        toolCalls: [],
        error: message,
        timedOut: isTimeout,
      };
    }
  }

  private buildProviderRequest(
    cfg: LlmInferenceConfig,
    messages: LlmChatMessage[],
    tools: LlmToolSchema[] | undefined,
    maxTokens: number,
  ): { endpoint: string; payload: Record<string, unknown> } {
    const mappedTools =
      tools && tools.length > 0
        ? tools.map((t) => ({
            type: 'function',
            function: {
              name: t.name,
              description: t.description,
              parameters: t.parameters,
            },
          }))
        : undefined;

    if (cfg.provider === 'ollama') {
      return {
        endpoint: `${cfg.baseUrl}/api/chat`,
        payload: {
          model: cfg.model,
          messages: messages.map((m) => this.mapOllamaMessage(m)),
          stream: false,
          options: {
            temperature: 0.2,
            num_predict: maxTokens,
          },
          ...(mappedTools ? { tools: mappedTools } : {}),
        },
      };
    }

    // openai_compatible and vllm both expose /v1/chat/completions
    return {
      endpoint: `${cfg.baseUrl}/v1/chat/completions`,
      payload: {
        model: cfg.model,
        messages: messages.map((m) => this.mapOpenAiMessage(m)),
        temperature: 0.2,
        max_tokens: maxTokens,
        stream: false,
        ...(mappedTools ? { tools: mappedTools, tool_choice: 'auto' } : {}),
      },
    };
  }

  private mapOpenAiMessage(m: LlmChatMessage): Record<string, unknown> {
    if (m.role === 'tool') {
      return {
        role: 'tool',
        tool_call_id: m.toolCallId ?? '',
        content: m.content,
      };
    }
    if (m.role === 'assistant' && m.toolCalls && m.toolCalls.length > 0) {
      return {
        role: 'assistant',
        content: m.content || null,
        tool_calls: m.toolCalls.map((tc) => ({
          id: tc.id,
          type: 'function',
          function: {
            name: tc.name,
            arguments: JSON.stringify(tc.arguments),
          },
        })),
      };
    }
    return {
      role: m.role,
      content: m.content,
    };
  }

  private mapOllamaMessage(m: LlmChatMessage): Record<string, unknown> {
    if (m.role === 'tool') {
      return {
        role: 'tool',
        content: m.content,
        ...(m.name ? { tool_name: m.name } : {}),
      };
    }
    if (m.role === 'assistant' && m.toolCalls && m.toolCalls.length > 0) {
      return {
        role: 'assistant',
        content: m.content,
        tool_calls: m.toolCalls.map((tc) => ({
          function: {
            name: tc.name,
            arguments: tc.arguments,
          },
        })),
      };
    }
    return {
      role: m.role,
      content: m.content,
    };
  }

  private parseProviderResponse(
    cfg: LlmInferenceConfig,
    rawBody: unknown,
  ): LlmGatewayResult {
    if (!rawBody || typeof rawBody !== 'object' || Array.isArray(rawBody)) {
      return this.malformedResponse(
        cfg,
        'Inference server returned a malformed response payload.',
      );
    }

    const body = rawBody as Record<string, unknown>;
    if (cfg.provider === 'ollama') {
      return this.parseOllamaResponse(cfg, body);
    }
    return this.parseOpenAiResponse(cfg, body);
  }

  private parseOpenAiResponse(
    cfg: LlmInferenceConfig,
    body: Record<string, unknown>,
  ): LlmGatewayResult {
    if (!Array.isArray(body.choices) || body.choices.length === 0) {
      return this.malformedResponse(
        cfg,
        'Inference server response is missing choices.',
      );
    }

    const first = body.choices[0] as Record<string, unknown> | undefined;
    const message = first?.message as Record<string, unknown> | undefined;
    if (!message || typeof message !== 'object') {
      return this.malformedResponse(
        cfg,
        'Inference server response is missing message.',
      );
    }

    const model =
      typeof body.model === 'string' && body.model.trim()
        ? body.model.trim()
        : cfg.model;
    const content =
      typeof message.content === 'string' ? message.content.trim() : '';
    const finishReason =
      typeof first?.finish_reason === 'string'
        ? first.finish_reason
        : undefined;

    const parsedCalls = this.parseToolCallsArray(message.tool_calls);
    if (!parsedCalls.ok) {
      return this.malformedResponse(cfg, parsedCalls.error);
    }

    if (parsedCalls.toolCalls.length === 0 && !content) {
      return this.malformedResponse(
        cfg,
        'Inference server returned an empty completion.',
      );
    }

    const usageRaw = body.usage as Record<string, unknown> | undefined;
    const usage = usageRaw
      ? {
          promptTokens:
            typeof usageRaw.prompt_tokens === 'number'
              ? usageRaw.prompt_tokens
              : undefined,
          completionTokens:
            typeof usageRaw.completion_tokens === 'number'
              ? usageRaw.completion_tokens
              : undefined,
          totalTokens:
            typeof usageRaw.total_tokens === 'number'
              ? usageRaw.total_tokens
              : undefined,
        }
      : undefined;

    return {
      ok: true,
      configured: true,
      provider: cfg.provider,
      content,
      model,
      toolCalls: parsedCalls.toolCalls,
      finishReason,
      usage,
    };
  }

  private parseOllamaResponse(
    cfg: LlmInferenceConfig,
    body: Record<string, unknown>,
  ): LlmGatewayResult {
    const message = body.message as Record<string, unknown> | undefined;
    if (!message || typeof message !== 'object') {
      return this.malformedResponse(
        cfg,
        'Ollama response is missing message object.',
      );
    }

    const model =
      typeof body.model === 'string' && body.model.trim()
        ? body.model.trim()
        : cfg.model;
    const content =
      typeof message.content === 'string' ? message.content.trim() : '';

    const parsedCalls = this.parseToolCallsArray(message.tool_calls);
    if (!parsedCalls.ok) {
      return this.malformedResponse(cfg, parsedCalls.error);
    }

    if (parsedCalls.toolCalls.length === 0 && !content) {
      return this.malformedResponse(
        cfg,
        'Inference server returned an empty completion.',
      );
    }

    return {
      ok: true,
      configured: true,
      provider: cfg.provider,
      content,
      model,
      toolCalls: parsedCalls.toolCalls,
      finishReason: typeof body.done_reason === 'string' ? body.done_reason : undefined,
      usage: {
        promptTokens:
          typeof body.prompt_eval_count === 'number'
            ? body.prompt_eval_count
            : undefined,
        completionTokens:
          typeof body.eval_count === 'number' ? body.eval_count : undefined,
      },
    };
  }

  private parseToolCallsArray(
    rawToolCalls: unknown,
  ):
    | { ok: true; toolCalls: LlmToolCallRequest[] }
    | { ok: false; error: string } {
    if (rawToolCalls === undefined || rawToolCalls === null) {
      return { ok: true, toolCalls: [] };
    }
    if (!Array.isArray(rawToolCalls)) {
      return {
        ok: false,
        error: 'Inference server returned non-array tool_calls.',
      };
    }

    const result: LlmToolCallRequest[] = [];
    for (let i = 0; i < rawToolCalls.length; i++) {
      const entry = rawToolCalls[i] as Record<string, unknown> | undefined;
      if (!entry || typeof entry !== 'object') {
        return { ok: false, error: 'Malformed tool_call entry.' };
      }

      const fn = entry.function as Record<string, unknown> | undefined;
      const name = typeof fn?.name === 'string' ? fn.name.trim() : '';
      if (!name) {
        return { ok: false, error: 'Tool call is missing function name.' };
      }

      const parsedArgs = this.parseStrictToolArguments(fn?.arguments);
      if (!parsedArgs.ok) {
        return {
          ok: false,
          error: `Malformed arguments for tool "${name}": ${parsedArgs.error}`,
        };
      }

      const id =
        typeof entry.id === 'string' && entry.id.trim()
          ? entry.id.trim()
          : `call_${i + 1}`;

      result.push({
        id,
        name,
        arguments: parsedArgs.value,
      });
    }

    return { ok: true, toolCalls: result };
  }

  private parseStrictToolArguments(
    rawArgs: unknown,
  ):
    | { ok: true; value: Record<string, unknown> }
    | { ok: false; error: string } {
    if (rawArgs === undefined || rawArgs === null || rawArgs === '') {
      return { ok: true, value: {} };
    }

    if (typeof rawArgs === 'string') {
      try {
        const parsed = JSON.parse(rawArgs) as unknown;
        if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
          return { ok: false, error: 'Arguments JSON must be an object.' };
        }
        return { ok: true, value: parsed as Record<string, unknown> };
      } catch {
        return { ok: false, error: 'Invalid JSON in tool arguments.' };
      }
    }

    if (typeof rawArgs === 'object' && !Array.isArray(rawArgs)) {
      return { ok: true, value: rawArgs as Record<string, unknown> };
    }

    return { ok: false, error: 'Tool arguments must be an object.' };
  }

  private malformedResponse(
    cfg: LlmInferenceConfig,
    error: string,
  ): LlmGatewayResult {
    return {
      ok: false,
      configured: true,
      provider: cfg.provider,
      content: '',
      model: cfg.model,
      toolCalls: [],
      error,
    };
  }
}
