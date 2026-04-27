import { Injectable } from '@nestjs/common';

export type DebugLogSource = 'backend' | 'portfolio' | 'partfinder-desktop';

export type DebugLogEntry = {
  id: string;
  ts: string;
  source: DebugLogSource;
  level: string;
  message: string;
  context?: string;
};

@Injectable()
export class DebugLogsService {
  private entries: DebugLogEntry[] = [];
  private seq = 0;
  private readonly maxEntries = 1000;

  push(
    source: DebugLogSource,
    level: string,
    message: string,
    context?: string,
  ): void {
    const entry: DebugLogEntry = {
      id: Date.now() + '-' + ++this.seq,
      ts: new Date().toISOString(),
      source,
      level,
      message,
      context,
    };
    this.entries.unshift(entry);
    if (this.entries.length > this.maxEntries) {
      this.entries.length = this.maxEntries;
    }
  }

  getAll(): DebugLogEntry[] {
    return [...this.entries];
  }

  clear(): void {
    this.entries = [];
  }
}
