import {
  Body,
  Controller,
  Delete,
  Get,
  Headers,
  Post,
  UnauthorizedException,
  UseGuards,
} from '@nestjs/common';
import { AuthGuard } from '@nestjs/passport';
import { ConfigService } from '@nestjs/config';
import { DebugLogsService } from './debug-logs.service';
import { AppendDebugLogsDto } from './dto/append-debug-logs.dto';
import { IngestDebugLogDto } from './dto/ingest-debug-log.dto';

@Controller('admin/debug/logs')
export class DebugController {
  constructor(
    private readonly debugLogs: DebugLogsService,
    private readonly config: ConfigService,
  ) {}

  @Get()
  @UseGuards(AuthGuard('jwt'))
  list() {
    return { logs: this.debugLogs.getAll() };
  }

  @Delete()
  @UseGuards(AuthGuard('jwt'))
  clear() {
    this.debugLogs.clear();
    return { ok: true };
  }

  @Post()
  @UseGuards(AuthGuard('jwt'))
  append(@Body() dto: AppendDebugLogsDto) {
    for (const item of dto.items) {
      this.debugLogs.push(item.source, item.level, item.message, item.context);
    }
    return { ok: true, accepted: dto.items.length };
  }

  @Post('ingest')
  ingest(
    @Headers('x-debug-log-ingest-key') key: string | undefined,
    @Body() dto: IngestDebugLogDto,
  ) {
    const expected = this.config.get<string>('DEBUG_LOG_INGEST_KEY')?.trim();
    if (!expected || key !== expected) {
      throw new UnauthorizedException('Invalid or missing ingest key');
    }
    const source = dto.source ?? 'partfinder-desktop';
    this.debugLogs.push(source, dto.level, dto.message, dto.context);
    return { ok: true };
  }
}
