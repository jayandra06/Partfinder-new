import { ConsoleLogger } from '@nestjs/common';
import { DebugLogsService } from './debug-logs.service';

function stringifyMessage(message: unknown): string {
  if (typeof message === 'string') {
    return message;
  }
  try {
    return JSON.stringify(message);
  } catch {
    return String(message);
  }
}

export class AggregatingLogger extends ConsoleLogger {
  constructor(private readonly logs: DebugLogsService) {
    super();
  }

  override log(message: unknown, context?: string) {
    this.logs.push('backend', 'info', stringifyMessage(message), context);
    super.log(message, context);
  }

  override error(message: unknown, stack?: string, context?: string) {
    const detail = [stack, context].filter(Boolean).join(' | ') || undefined;
    this.logs.push('backend', 'error', stringifyMessage(message), detail);
    super.error(message, stack, context);
  }

  override warn(message: unknown, context?: string) {
    this.logs.push('backend', 'warn', stringifyMessage(message), context);
    super.warn(message, context);
  }

  override debug(message: unknown, context?: string) {
    this.logs.push('backend', 'debug', stringifyMessage(message), context);
    super.debug(message, context);
  }

  override verbose(message: unknown, context?: string) {
    this.logs.push('backend', 'verbose', stringifyMessage(message), context);
    super.verbose(message, context);
  }
}
