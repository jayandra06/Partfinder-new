import type { AirsIngestFrame } from './airs-ingest.js';

export type DopplerBin = { rangeM: number; velocityMs: number; powerDb: number };

/**
 * Ring buffer of synthetic Doppler bins — sandbox only.
 */
export class DopplerRingBuffer {
  private readonly capacity: number;
  private bins: DopplerBin[][] = [];
  private head = 0;

  constructor(capacity: number) {
    this.capacity = Math.max(4, capacity);
  }

  push(frame: AirsIngestFrame): number {
    const row: DopplerBin[] = frame.points.map((p, i) => ({
      rangeM: 500 + i * 75,
      velocityMs: (p.rcsDbsm % 7) - 3,
      powerDb: p.rcsDbsm,
    }));
    if (this.bins.length < this.capacity) {
      this.bins.push(row);
    } else {
      this.bins[this.head] = row;
      this.head = (this.head + 1) % this.capacity;
    }
    return row.length;
  }

  flatten(): DopplerBin[] {
    return this.bins.flat();
  }
}
