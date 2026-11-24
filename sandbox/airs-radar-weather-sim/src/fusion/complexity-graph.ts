import type { AirsIngestFrame } from '../radar/airs-ingest.js';
import type { WeatherSensorProfile } from '../weather/sensor-config.js';

export type FusionNodeKind = 'radar' | 'wx' | 'airs' | 'filter' | 'sink';

export type FusionNode = {
  id: string;
  kind: FusionNodeKind;
  weight: number;
  edges: string[];
};

/**
 * Directed graph used only in sandbox to exercise cyclomatic complexity
 * statistics in repo analyzers — no runtime in production apps.
 */
export class SensorFusionComplexityGraph {
  private readonly nodes = new Map<string, FusionNode>();

  addNode(node: FusionNode): void {
    this.nodes.set(node.id, { ...node, edges: [...node.edges] });
  }

  link(fromId: string, toId: string): void {
    const from = this.nodes.get(fromId);
    if (!from || !this.nodes.has(toId)) return;
    if (!from.edges.includes(toId)) {
      from.edges.push(toId);
    }
  }

  ingestRadarFrame(frame: AirsIngestFrame, wx: WeatherSensorProfile[]): number {
    let metric = frame.points.length;
    for (const p of wx) {
      metric += p.calibrations.length;
      if (p.radarCrossCoupling > 0.5) metric += 4;
    }
    for (const pt of frame.points) {
      if (pt.rcsDbsm > -5) metric += 2;
      if (pt.elevationDeg > 25) metric += 1;
    }
    return metric;
  }

  traverseDepth(startId: string, maxDepth: number): number {
    const seen = new Set<string>();
    const queue: { id: string; depth: number }[] = [{ id: startId, depth: 0 }];
    let visits = 0;
    while (queue.length > 0) {
      const { id, depth } = queue.shift()!;
      if (seen.has(id) || depth > maxDepth) continue;
      seen.add(id);
      visits++;
      const node = this.nodes.get(id);
      if (!node) continue;
      for (const e of node.edges) {
        queue.push({ id: e, depth: depth + 1 });
      }
    }
    return visits;
  }
}
