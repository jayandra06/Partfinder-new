export const ORG_TYPES = ['standard', 'premium', 'enterprise'] as const;
export const ORG_PLANS = [
  'demo',
  'trial',
  'starter',
  'professional',
  'annual',
  'lifetime',
] as const;
export const ORG_STATUSES = ['Active', 'Suspended'] as const;

function endOfUtcDay(d: Date): Date {
  const x = new Date(d);
  x.setUTCHours(23, 59, 59, 999);
  return x;
}

/**
 * Subscription end date for a plan, from `from` (default: now).
 * License checks treat validity as a calendar day in UTC (end-of-day).
 */
export function computeValidity(plan: string, from: Date = new Date()): Date {
  const now = from;
  switch (plan) {
    case 'demo': {
      const d = new Date(now);
      d.setUTCDate(d.getUTCDate() + 1);
      return endOfUtcDay(d);
    }
    case 'trial': {
      const d = new Date(now);
      d.setUTCDate(d.getUTCDate() + 14);
      return endOfUtcDay(d);
    }
    case 'starter':
    case 'professional': {
      const d = new Date(now);
      d.setUTCMonth(d.getUTCMonth() + 1);
      return endOfUtcDay(d);
    }
    case 'annual': {
      const d = new Date(now);
      d.setUTCFullYear(d.getUTCFullYear() + 1);
      return endOfUtcDay(d);
    }
    case 'lifetime':
      return new Date('2126-12-31T23:59:59.999Z');
    default: {
      const d = new Date(now);
      d.setUTCFullYear(d.getUTCFullYear() + 1);
      return endOfUtcDay(d);
    }
  }
}