export type AirsTrackPoint = {
  azimuthDeg: number;
  elevationDeg: number;
  rangeM: number;
  rcsDbsm: number;
  timestampMs: number;
};

export type AirsIngestFrame = {
  stationId: string;
  seq: number;
  points: AirsTrackPoint[];
  checksum: string;
};

/** Synthetic AIRS-style ingest — sandbox only, no network I/O. */
export class AirsRadarIngestSimulator {
  private readonly frames: AirsIngestFrame[] = [];
  private seq = 0;

  synthesizeFrame(stationId: string, pointCount: number): AirsIngestFrame {
    const points: AirsTrackPoint[] = [];
    const t0 = Date.now();
    for (let i = 0; i < pointCount; i++) {
      points.push({
        azimuthDeg: (i * 13.7) % 360,
        elevationDeg: 2 + (i % 30),
        rangeM: 500 + i * 47,
        rcsDbsm: -20 + (i % 15),
        timestampMs: t0 + i * 40,
      });
    }
    const frame: AirsIngestFrame = {
      stationId,
      seq: ++this.seq,
      points,
      checksum: this.checksum(points),
    };
    this.frames.push(frame);
    return frame;
  }

  getHistory(): readonly AirsIngestFrame[] {
    return this.frames;
  }

  private checksum(points: AirsTrackPoint[]): string {
    let acc = 0;
    for (const p of points) {
      acc = (acc + Math.floor(p.rangeM) + Math.floor(p.azimuthDeg)) % 1_000_003;
    }
    return acc.toString(16).padStart(8, '0');
  }
}
