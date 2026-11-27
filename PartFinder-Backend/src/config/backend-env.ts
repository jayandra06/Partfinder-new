import { existsSync, readFileSync } from 'fs';
import { join } from 'path';

/** PartFinder-Backend package root (independent of shell cwd). */
export function backendRootDir(): string {
  let dir = __dirname;
  for (let depth = 0; depth < 6; depth++) {
    const pkgPath = join(dir, 'package.json');
    if (existsSync(pkgPath)) {
      try {
        const pkg = JSON.parse(readFileSync(pkgPath, 'utf8')) as { name?: string };
        if (pkg.name === 'PartFinder-Backend') {
          return dir;
        }
      } catch {
        /* try parent */
      }
    }
    dir = join(dir, '..');
  }
  return join(__dirname, '..', '..');
}

/** `.env` paths under the backend package, not the shell cwd. */
export function backendEnvFilePaths(): string[] {
  const root = backendRootDir();
  const paths = [join(root, '.env.local'), join(root, '.env')];
  const existing = paths.filter((p) => existsSync(p));
  return existing.length > 0 ? existing : [join(root, '.env')];
}

export const SUPPORTED_LLM_PROVIDERS = [
  'openai_compatible',
  'vllm',
  'ollama',
] as const;

export type LlmProviderType = (typeof SUPPORTED_LLM_PROVIDERS)[number];

export interface LlmInferenceConfig {
  provider: LlmProviderType;
  baseUrl: string;
  model: string;
  secret: string;
  timeoutMs: number;
  maxTokens: number;
  configured: boolean;
  configError?: string;
}

export function resolveLlmInferenceConfig(
  getEnv: (key: string) => string | undefined,
): LlmInferenceConfig {
  const rawProvider = (getEnv('LLM_PROVIDER') ?? 'openai_compatible')
    .trim()
    .toLowerCase();
  const providerValid = (SUPPORTED_LLM_PROVIDERS as readonly string[]).includes(
    rawProvider,
  );
  const provider: LlmProviderType = providerValid
    ? (rawProvider as LlmProviderType)
    : 'openai_compatible';

  const rawBaseUrl = (getEnv('LLM_INFERENCE_BASE_URL') ?? '')
    .trim()
    .replace(/\/+$/, '');
  const model = (getEnv('LLM_MODEL') ?? '').trim();
  const secret = (getEnv('LLM_INFERENCE_SECRET') ?? '').trim();

  const rawTimeout = Number(getEnv('LLM_TIMEOUT_MS') ?? '20000');
  const timeoutMs =
    Number.isFinite(rawTimeout) && rawTimeout >= 1000 && rawTimeout <= 120000
      ? Math.floor(rawTimeout)
      : 20000;

  const rawMaxTokens = Number(getEnv('LLM_MAX_TOKENS') ?? '1024');
  const maxTokens =
    Number.isFinite(rawMaxTokens) && rawMaxTokens >= 64 && rawMaxTokens <= 16384
      ? Math.floor(rawMaxTokens)
      : 1024;

  if (!providerValid) {
    return {
      provider,
      baseUrl: '',
      model,
      secret,
      timeoutMs,
      maxTokens,
      configured: false,
      configError: `Unsupported LLM_PROVIDER "${rawProvider}". Supported providers: ${SUPPORTED_LLM_PROVIDERS.join(', ')}.`,
    };
  }

  let baseUrl = '';
  if (rawBaseUrl) {
    try {
      const parsed = new URL(rawBaseUrl);
      if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
        return {
          provider,
          baseUrl: '',
          model,
          secret,
          timeoutMs,
          maxTokens,
          configured: false,
          configError: 'LLM_INFERENCE_BASE_URL must use http or https.',
        };
      }
      baseUrl = rawBaseUrl;
    } catch {
      return {
        provider,
        baseUrl: '',
        model,
        secret,
        timeoutMs,
        maxTokens,
        configured: false,
        configError: 'LLM_INFERENCE_BASE_URL is not a valid URL.',
      };
    }
  }

  return {
    provider,
    baseUrl,
    model,
    secret,
    timeoutMs,
    maxTokens,
    configured: Boolean(baseUrl && model),
  };
}

export interface AiConversationContextConfig {
  historyWindowMessages: number;
  maxStoredMessages: number;
  summaryThresholdMessages: number;
  maxSummaryChars: number;
  maxPromptHistoryChars: number;
}

function boundedInt(
  raw: string | undefined,
  fallback: number,
  min: number,
  max: number,
): number {
  const n = Number(raw);
  if (!Number.isFinite(n)) return fallback;
  const floored = Math.floor(n);
  return floored >= min && floored <= max ? floored : fallback;
}

export function resolveAiContextConfig(
  getEnv: (key: string) => string | undefined,
): AiConversationContextConfig {
  const historyWindowMessages = boundedInt(
    getEnv('AI_CONTEXT_WINDOW_MESSAGES'),
    12,
    2,
    40,
  );
  const maxStoredMessages = Math.max(
    historyWindowMessages,
    boundedInt(getEnv('AI_MAX_STORED_MESSAGES'), 40, 10, 200),
  );
  const summaryThresholdMessages = boundedInt(
    getEnv('AI_SUMMARY_THRESHOLD_MESSAGES'),
    16,
    4,
    100,
  );
  const maxSummaryChars = boundedInt(
    getEnv('AI_MAX_SUMMARY_CHARS'),
    2000,
    300,
    6000,
  );
  const maxPromptHistoryChars = boundedInt(
    getEnv('AI_MAX_PROMPT_HISTORY_CHARS'),
    16000,
    2000,
    64000,
  );

  return {
    historyWindowMessages,
    maxStoredMessages,
    summaryThresholdMessages,
    maxSummaryChars,
    maxPromptHistoryChars,
  };
}
