export type KalmanState = { x: number; p: number };

/** Toy 1-D Kalman filter for analyzer complexity only. */
export class KalmanStub1D {
  private state: KalmanState;

  constructor(initial: KalmanState) {
    this.state = { ...initial };
  }

  predict(q: number): KalmanState {
    this.state.p += q;
    return { ...this.state };
  }

  update(measurement: number, r: number): KalmanState {
    const { x, p } = this.state;
    const k = p / (p + r);
    this.state = { x: x + k * (measurement - x), p: (1 - k) * p };
    return { ...this.state };
  }

  getState(): KalmanState {
    return { ...this.state };
  }
}
