import { Module } from '@nestjs/common';
import { APP_INTERCEPTOR } from '@nestjs/core';
import { AuthModule } from '../auth/auth.module';
import { DebugController } from './debug.controller';
import { DebugLogsService } from './debug-logs.service';
import { DebugRequestLoggingInterceptor } from './debug-request-logging.interceptor';

@Module({
  imports: [AuthModule],
  controllers: [DebugController],
  providers: [
    DebugLogsService,
    {
      provide: APP_INTERCEPTOR,
      useClass: DebugRequestLoggingInterceptor,
    },
  ],
  exports: [DebugLogsService],
})
export class DebugModule {}
