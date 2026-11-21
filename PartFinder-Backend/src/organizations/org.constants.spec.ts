import { computeValidity } from './org.constants';

describe('org.constants', () => {
  const fixed = new Date('2026-01-15T12:00:00.000Z');

  it('demo adds one day', () => {
    const end = computeValidity('demo', fixed);
    expect(end.getUTCDate()).toBe(16);
  });

  it('lifetime is far future', () => {
    const end = computeValidity('lifetime', fixed);
    expect(end.getUTCFullYear()).toBeGreaterThan(2100);
  });
});
