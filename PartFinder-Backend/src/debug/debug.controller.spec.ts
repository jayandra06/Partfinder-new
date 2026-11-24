import { ConsoleLogger, UnauthorizedException } from '@nestjs/common';
import { AggregatingLogger } from './aggregating-logger';
import { DebugController } from './debug.controller';
import { DebugLogsService } from './debug-logs.service';

function controller(ingestKey?: string) {
  const logs = new DebugLogsService();
  const config = { get: jest.fn().mockReturnValue(ingestKey) };
  return { logs, ctrl: new DebugController(logs, config as any) };
}

describe('DebugController', () => {
  it('append stores items newest-first and reports how many were accepted', () => {
    const { ctrl } = controller();

    const res = ctrl.append({
      items: [
        { source: 'portfolio', level: 'info', message: 'first' },
        { source: 'portfolio', level: 'error', message: 'second', context: 'ctx' },
      ],
    } as any);

    expect(res).toEqual({ ok: true, accepted: 2 });
    const { logs } = ctrl.list();
    expect(logs.map((l) => l.message)).toEqual(['second', 'first']);
    expect(logs[0]).toMatchObject({ source: 'portfolio', level: 'error', context: 'ctx' });
  });

  it('clear empties the log', () => {
    const { ctrl } = controller();
    ctrl.append({ items: [{ source: 'backend', level: 'info', message: 'x' }] } as any);

    expect(ctrl.clear()).toEqual({ ok: true });
    expect(ctrl.list().logs).toHaveLength(0);
  });

  it('ingest rejects when no key is configured', () => {
    const { ctrl } = controller(undefined);

    expect(() => ctrl.ingest('anything', { level: 'info', message: 'm' } as any)).toThrow(UnauthorizedException);
  });

  it('ingest rejects a wrong key', () => {
    const { ctrl } = controller('secret');

    expect(() => ctrl.ingest('nope', { level: 'info', message: 'm' } as any)).toThrow(UnauthorizedException);
  });

  it('ingest accepts the configured key (trimmed) and defaults the source', () => {
    const { ctrl, logs } = controller('  secret  ');

    expect(ctrl.ingest('secret', { level: 'warn', message: 'from desktop' } as any)).toEqual({ ok: true });
    expect(logs.getAll()[0]).toMatchObject({ source: 'partfinder-desktop', level: 'warn', message: 'from desktop' });
  });
});

describe('DebugLogsService', () => {
  it('caps the buffer at 1000 entries, keeping the newest', () => {
    const logs = new DebugLogsService();
    for (let i = 0; i < 1005; i++) logs.push('backend', 'info', `m${i}`);

    const all = logs.getAll();
    expect(all).toHaveLength(1000);
    expect(all[0].message).toBe('m1004');
    expect(all[999].message).toBe('m5');
  });

  it('gives every entry a unique id and ISO timestamp', () => {
    const logs = new DebugLogsService();
    logs.push('backend', 'info', 'a');
    logs.push('backend', 'info', 'b');

    const [b, a] = logs.getAll();
    expect(a.id).not.toBe(b.id);
    expect(new Date(a.ts).toISOString()).toBe(a.ts);
  });

  it('getAll returns a copy', () => {
    const logs = new DebugLogsService();
    logs.push('backend', 'info', 'a');

    logs.getAll().pop();

    expect(logs.getAll()).toHaveLength(1);
  });
});

describe('AggregatingLogger', () => {
  beforeEach(() => {
    for (const m of ['log', 'error', 'warn', 'debug', 'verbose'] as const) {
      jest.spyOn(ConsoleLogger.prototype, m).mockImplementation(() => undefined);
    }
  });
  afterEach(() => jest.restoreAllMocks());

  it('mirrors every level into the debug log buffer', () => {
    const logs = new DebugLogsService();
    const logger = new AggregatingLogger(logs);

    logger.log('l', 'Ctx');
    logger.warn('w');
    logger.debug('d');
    logger.verbose('v');
    logger.error('e', 'stack', 'Ctx');

    expect(logs.getAll().map((e) => e.level)).toEqual(['error', 'verbose', 'debug', 'warn', 'info']);
    expect(logs.getAll()[0].context).toBe('stack | Ctx');
    expect(logs.getAll()[4].context).toBe('Ctx');
  });

  it('stringifies non-string messages', () => {
    const logs = new DebugLogsService();
    new AggregatingLogger(logs).log({ a: 1 });

    expect(logs.getAll()[0].message).toBe('{"a":1}');
  });
});
