/**
 * Keep in sync with PartFinder-Backend:
 * - org.constants.ts (plan ids + validity rules)
 * - license-purchase.service.ts → LICENSE_PRICE_USD_CENTS (1000 = $10)
 * Self-serve checkout provisions the `starter` plan.
 */

export const SELF_SERVE_PRICE_USD = 10
export const SELF_SERVE_PRICE_LABEL = `$${SELF_SERVE_PRICE_USD} USD`
export const SELF_SERVE_PLAN_ID = 'starter'

/** Default org limits applied on create (organizations.service.ts). */
export const DEFAULT_MAX_USERS = 50
export const DEFAULT_MAX_PARTS = 100_000

export type PlanId =
  | 'demo'
  | 'trial'
  | 'starter'
  | 'professional'
  | 'annual'
  | 'lifetime'

export type PricingPlan = {
  id: PlanId
  name: string
  billingPeriod: string
  validityNote: string
  summary: string
  /** Shown price; null = contact sales / admin-assigned only */
  priceUsd: number | null
  selfServe: boolean
  highlighted?: boolean
}

/** Subscription plans enforced by licence verification (backend). */
export const SUBSCRIPTION_PLANS: PricingPlan[] = [
  {
    id: 'demo',
    name: 'Demo',
    billingPeriod: 'Evaluation',
    validityNote: '1 calendar day (UTC)',
    summary: 'Short prospect evaluation. Issued by a platform administrator.',
    priceUsd: null,
    selfServe: false,
  },
  {
    id: 'trial',
    name: 'Trial',
    billingPeriod: 'Trial',
    validityNote: '14 days full access',
    summary: 'Full-feature trial before commitment. Issued by a platform administrator.',
    priceUsd: null,
    selfServe: false,
  },
  {
    id: 'starter',
    name: 'Starter',
    billingPeriod: 'Monthly',
    validityNote: '1 month from activation (UTC end-of-day)',
    summary:
      'Small vendor or single-site team. Buy online — same starter plan activated after Razorpay payment on Get Licence.',
    priceUsd: SELF_SERVE_PRICE_USD,
    selfServe: true,
    highlighted: true,
  },
  {
    id: 'professional',
    name: 'Professional',
    billingPeriod: 'Monthly',
    validityNote: '1 month from activation (UTC end-of-day)',
    summary: 'Larger vendor or multi-team operations. Contact sales for a quote.',
    priceUsd: null,
    selfServe: false,
  },
  {
    id: 'annual',
    name: 'Annual',
    billingPeriod: 'Yearly',
    validityNote: '12 months from activation',
    summary: 'Yearly subscription with predictable renewal. Contact sales.',
    priceUsd: null,
    selfServe: false,
  },
  {
    id: 'lifetime',
    name: 'Lifetime',
    billingPeriod: 'Perpetual',
    validityNote: 'Long-dated licence (special agreements)',
    summary: 'Perpetual licence for strategic accounts. Contact sales.',
    priceUsd: null,
    selfServe: false,
  },
]

export type OrgTierId = 'standard' | 'premium' | 'enterprise'

export type OrgTier = {
  id: OrgTierId
  name: string
  summary: string
}

/** Organisation type (tier) on create — separate from subscription plan (backend ORG_TYPES). */
export const ORG_TIERS: OrgTier[] = [
  {
    id: 'standard',
    name: 'Standard',
    summary: 'Single location / smaller catalogue footprint.',
  },
  {
    id: 'premium',
    name: 'Premium',
    summary: 'Multi-branch operations and advanced workflows.',
  },
  {
    id: 'enterprise',
    name: 'Enterprise',
    summary: 'OEM, distributor, or multi-org scale — custom limits and deployment.',
  },
]
