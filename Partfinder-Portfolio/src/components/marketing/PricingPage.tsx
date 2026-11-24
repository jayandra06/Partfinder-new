import { Link } from 'react-router-dom'
import {
  DEFAULT_MAX_PARTS,
  DEFAULT_MAX_USERS,
  ORG_TIERS,
  SELF_SERVE_PRICE_LABEL,
  SUBSCRIPTION_PLANS,
} from './pricing.constants'
import { Icon, MarketingFrame } from './SiteChrome'

export function PricingPage() {
  return (
    <MarketingFrame>
      <main className="w-full pt-20 bg-background min-h-[calc(100vh-5rem)]">
        <section className="max-w-[1360px] mx-auto px-gutter py-space-lg pb-space-xl">
          <div className="flex items-center gap-space-xs text-primary font-mono-data text-mono-data tracking-widest uppercase bg-surface-container px-space-sm py-1 rounded w-fit mb-space-md">
            <span className="inline-block w-2 h-2 rounded-full bg-primary animate-pulse" />
            <span>PRICING</span>
          </div>
          <div className="max-w-3xl mb-space-xl">
            <h1 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight m-0">
              PartFinder licensing
            </h1>
            <p className="font-body-md text-body-md text-on-surface-variant mt-space-sm m-0">
              Plans match what the PartFinder backend enforces on your organisation code. Today,{' '}
              <strong className="text-on-surface">only Starter</strong> is available to buy online at{' '}
              {SELF_SERVE_PRICE_LABEL} via Get Licence (Razorpay). Other plans are assigned by your
              administrator or sales team — same app, same features, different subscription length.
            </p>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-gutter-lg mb-space-xl">
            {SUBSCRIPTION_PLANS.map((plan) => (
              <article
                key={plan.id}
                className={
                  plan.highlighted
                    ? 'rounded-xl p-space-lg shadow-md border-2 border-primary bg-surface-container-lowest flex flex-col gap-space-sm'
                    : 'rounded-xl p-space-lg shadow-sm bg-surface-container-lowest flex flex-col gap-space-sm'
                }
              >
                {plan.highlighted ? (
                  <span className="font-caption text-caption text-on-primary bg-primary px-2 py-0.5 rounded w-fit font-bold uppercase tracking-wider">
                    Buy online
                  </span>
                ) : null}
                <h2 className="font-headline-sm text-headline-sm text-on-surface font-extrabold m-0">
                  {plan.name}
                </h2>
                <p className="font-mono-data text-mono-data text-primary m-0">
                  {plan.priceUsd != null ? (
                    <>
                      <span className="font-headline-md text-headline-md font-extrabold">{SELF_SERVE_PRICE_LABEL}</span>
                      <span className="text-on-surface-variant"> / {plan.billingPeriod.toLowerCase()}</span>
                    </>
                  ) : (
                    <span className="text-on-surface-variant font-label-md text-label-md">Contact sales</span>
                  )}
                </p>
                <p className="font-caption text-caption text-outline m-0">{plan.validityNote}</p>
                <p className="font-body-sm text-body-sm text-on-surface-variant m-0 flex-grow">{plan.summary}</p>
                <ul className="font-body-sm text-body-sm text-on-surface m-0 pl-4 flex flex-col gap-1">
                  <li>Up to {DEFAULT_MAX_USERS} users (default org limit)</li>
                  <li>Up to {DEFAULT_MAX_PARTS.toLocaleString()} parts (default org limit)</li>
                  <li>PartFinder Windows app + API access</li>
                </ul>
                {plan.selfServe ? (
                  <Link
                    to="/get-licence"
                    className="mt-space-sm bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors inline-flex items-center justify-center gap-1.5 shadow-sm"
                  >
                    Get Licence — {SELF_SERVE_PRICE_LABEL}
                    <Icon name="arrow_forward" className="text-[18px]" />
                  </Link>
                ) : (
                  <Link
                    to="/#architect"
                    className="mt-space-sm bg-surface-container-high hover:bg-surface-container-highest text-on-surface font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors inline-flex items-center justify-center gap-1.5"
                  >
                    Contact sales
                    <Icon name="mail" className="text-[18px]" />
                  </Link>
                )}
              </article>
            ))}
          </div>

          <div className="bg-surface-container-low rounded-xl p-space-lg md:p-space-xl mb-space-xl">
            <h2 className="font-headline-md text-headline-md text-on-surface font-extrabold m-0 mb-space-md">
              Organisation tier (deployment scale)
            </h2>
            <p className="font-body-sm text-body-sm text-on-surface-variant m-0 mb-space-lg">
              Separate from subscription plan: when an org is created, it also has a type — standard,
              premium, or enterprise — describing operational scale. Limits can be adjusted by platform
              admins.
            </p>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-gutter">
              {ORG_TIERS.map((tier) => (
                <div key={tier.id} className="bg-surface-container-lowest rounded-lg p-space-md shadow-sm">
                  <h3 className="font-label-md text-label-md text-primary font-bold m-0 capitalize">{tier.name}</h3>
                  <p className="font-body-sm text-body-sm text-on-surface-variant mt-2 m-0">{tier.summary}</p>
                </div>
              ))}
            </div>
          </div>

          <div className="bg-primary/5 border border-primary/20 rounded-xl p-space-lg flex flex-col md:flex-row md:items-center justify-between gap-space-md">
            <div>
              <h2 className="font-headline-sm text-headline-sm text-on-surface font-extrabold m-0">
                Ready to start with Starter?
              </h2>
              <p className="font-body-sm text-body-sm text-on-surface-variant mt-1 m-0">
                Pay {SELF_SERVE_PRICE_LABEL}, receive your 6-digit organisation code and temporary admin
                password, then set up the Windows app.
              </p>
            </div>
            <Link
              to="/get-licence"
              className="shrink-0 bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors inline-flex items-center gap-1.5 shadow-sm"
            >
              Get Licence
              <Icon name="payments" className="text-[18px]" />
            </Link>
          </div>

          <p className="font-caption text-caption text-outline text-center mt-space-lg m-0">
            Prices shown reflect the current self-serve checkout. Enterprise quotes may include custom
            plans, annual billing, or lifetime agreements not listed here.
          </p>
        </section>
      </main>
    </MarketingFrame>
  )
}
