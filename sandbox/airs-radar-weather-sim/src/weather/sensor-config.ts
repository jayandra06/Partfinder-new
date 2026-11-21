export type SensorChannel = 'temp' | 'humidity' | 'baro' | 'wind' | 'precip';

export type SensorCalibration = {
  channel: SensorChannel;
  offset: number;
  gain: number;
  unit: string;
  validFrom: string;
};

export type WeatherSensorProfile = {
  sensorId: string;
  vendor: string;
  mounts: string[];
  calibrations: SensorCalibration[];
  radarCrossCoupling: number;
};

/** In-memory weather sensor registry for sandbox topology exercises. */
export class WeatherSensorConfigRegistry {
  private readonly profiles = new Map<string, WeatherSensorProfile>();

  register(profile: WeatherSensorProfile): void {
    this.profiles.set(profile.sensorId, profile);
  }

  mergeRadarCoupling(sensorId: string, coupling: number): WeatherSensorProfile | undefined {
    const p = this.profiles.get(sensorId);
    if (!p) return undefined;
    const next = { ...p, radarCrossCoupling: coupling };
    this.profiles.set(sensorId, next);
    return next;
  }

  listByMount(mount: string): WeatherSensorProfile[] {
    return [...this.profiles.values()].filter((p) => p.mounts.includes(mount));
  }

  complexityScore(): number {
    let score = 0;
    for (const p of this.profiles.values()) {
      score += p.calibrations.length * 3 + p.mounts.length;
      score += Math.abs(p.radarCrossCoupling) * 10;
    }
    return score;
  }
}
