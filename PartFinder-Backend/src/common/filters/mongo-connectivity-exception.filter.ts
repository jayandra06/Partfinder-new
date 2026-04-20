import { ArgumentsHost, Catch, HttpStatus } from '@nestjs/common';
import { BaseExceptionFilter } from '@nestjs/core';
import { AbstractHttpAdapter } from '@nestjs/core/adapters/http-adapter';

const MONGO_CONNECTIVITY_NAMES = new Set([
  'MongoServerSelectionError',
  'MongoNetworkError',
  'MongoTimeoutError',
]);

function isMongoConnectivityError(exception: unknown): boolean {
  if (!exception || typeof exception !== 'object') return false;
  const name = (exception as Error).name;
  return MONGO_CONNECTIVITY_NAMES.has(name);
}

function connectivityUserMessage(exception: unknown): string {
  const base =
    'Database is temporarily unreachable; the API cannot query MongoDB. Check internet, DNS, VPN, firewall, that Atlas is not paused, and IP allowlist.';
  if (!exception || typeof exception !== 'object') return base;
  const err = exception as Error & { cause?: unknown };
  const cause = err.cause;
  if (cause && typeof cause === 'object' && 'code' in cause) {
    const code = (cause as { code?: string }).code;
    if (code === 'ENOTFOUND') {
      return `${base} DNS could not resolve the database host (ENOTFOUND).`;
    }
  }
  return base;
}

@Catch()
export class MongoConnectivityExceptionFilter extends BaseExceptionFilter {
  constructor(applicationRef: AbstractHttpAdapter) {
    super(applicationRef);
  }

  catch(exception: unknown, host: ArgumentsHost) {
    if (isMongoConnectivityError(exception)) {
      const ctx = host.switchToHttp();
      const response = ctx.getResponse();
      const applicationRef = this.applicationRef as AbstractHttpAdapter;

      const body = {
        statusCode: HttpStatus.SERVICE_UNAVAILABLE,
        message: connectivityUserMessage(exception),
        error: 'Service Unavailable',
      };

      if (applicationRef && !applicationRef.isHeadersSent(response)) {
        applicationRef.reply(response, body, HttpStatus.SERVICE_UNAVAILABLE);
      }
      return;
    }
    super.catch(exception, host);
  }
}
