import {
  CallHandler,
  ExecutionContext,
  Injectable,
  NestInterceptor,
} from '@nestjs/common';
import { Request } from 'express';
import { Observable, catchError, tap, throwError } from 'rxjs';
import { DebugLogsService } from './debug-logs.service';

@Injectable()
export class DebugRequestLoggingInterceptor implements NestInterceptor {
  constructor(private readonly debugLogs: DebugLogsService) {}

  intercept(context: ExecutionContext, next: CallHandler): Observable<unknown> {
    if (context.getType() !== 'http') {
      return next.handle();
    }
    const req = context.switchToHttp().getRequest<Request>();
    const url = (req.originalUrl ?? req.url ?? '').split('?')[0];
    if (url.includes('/admin/debug/logs')) {
      return next.handle();
    }
    const line = req.method + ' ' + url;
    return next.handle().pipe(
      tap(() => {
        this.debugLogs.push('backend', 'http', line);
      }),
      catchError((err: unknown) => {
        const msg = err instanceof Error ? err.message : String(err);
        this.debugLogs.push('backend', 'error', line + ' - ' + msg);
        return throwError(() => err);
      }),
    );
  }
}
