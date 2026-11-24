import { useEffect, useMemo, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { Icon, MarketingFrame } from './SiteChrome'

/** Verticals marketed on the portfolio (footer + industries page). */
export type IndustryId =
  | 'marine'
  | 'automotive'
  | 'aviation'
  | 'industrial-plants'
  | 'electronics'

type Industry = {
  id: IndustryId
  name: string
  headline: string
  summary: string
  icon: string
  tags: string[]
  highlights: string[]
  relatedSolutions: string[]
  relatedProductIds: string[]
}

export const INDUSTRIES: Industry[] = [
  {
    id: 'marine',
    name: 'Marine',
    headline: 'Fleet, vessel, and drydock operations',
    summary:
      'Align shore-side procurement with IMPA and ISSA catalogue standards, voyage manifests, and bonded logistics for global shipping and offshore fleets.',
    icon: 'directions_boat',
    tags: ['impa', 'issa', 'fleet', 'vessel', 'drydock', 'shipping', 'offshore'],
    highlights: [
      'IMPA / ISSA code matching and bonded bunker routing',
      'Voyage-centric spares and port turnaround visibility',
      'Case pattern: fleet RFQ and manifest readiness (Solutions SYS-SOL-01)',
    ],
    relatedSolutions: ['Fleet & Vessel Management', 'Procurement & Supply Chain'],
    relatedProductIds: ['fleet-vessel', 'supplier-network', 'ai-smart-search'],
  },
  {
    id: 'automotive',
    name: 'Automotive',
    headline: 'OEM, aftermarket, and multi-site parts networks',
    summary:
      'Unify part numbers, supersessions, and supplier RFQs across plants and distributors — built for high-volume catalogues and tiered supply chains.',
    icon: 'directions_car',
    tags: ['oem', 'aftermarket', 'vin', 'supersession', 'tier-1', 'distributor'],
    highlights: [
      'OEM traceability and certified alternate matching',
      'Multi-organisation governance for regional subsidiaries',
      'Enterprise org tier for distributor-scale catalogues (platform admin)',
    ],
    relatedSolutions: ['OEM & Aftermarket Support', 'Multi-Organisation Governance'],
    relatedProductIds: ['technical-specs', 'supplier-network', 'windows-app'],
  },
  {
    id: 'aviation',
    name: 'Aviation',
    headline: 'Aviation and defence reliability programmes',
    summary:
      'Support conformity, traceability, and counterfeit screening workflows for rotable components and regulated maintenance environments.',
    icon: 'flight',
    tags: ['aviation', 'defence', 'faa', 'easa', '8130-3', 'nsn', 'itar'],
    highlights: [
      'FAA / EASA conformity and Form 8130-3 tagging patterns',
      'Crypto-signed certificate of conformance tracking',
      'Primary operating domain: Aviation (home page architect form)',
    ],
    relatedSolutions: ['OEM & Aftermarket Support', 'Maintenance & Repair Operations'],
    relatedProductIds: ['technical-specs', 'inventory-intelligence', 'ai-smart-search'],
  },
  {
    id: 'industrial-plants',
    name: 'Industrial & Plants',
    headline: 'Plant floor MRO and high-throughput manufacturing',
    summary:
      'Give maintenance and stores teams barcode-friendly search, PM kitting, and downtime triage integrated with CMMS and ERP patterns.',
    icon: 'factory',
    tags: ['mro', 'plant', 'manufacturing', 'pm', 'cmms', 'sap', 'maximo', 'kitting'],
    highlights: [
      'Preventative maintenance BOM kitting and MTTR reduction',
      'Offline-capable field identification (Solutions SYS-SOL-02)',
      'Primary operating domain: Plants (home page architect form)',
    ],
    relatedSolutions: ['Maintenance & Repair Operations', 'Data & Predictive Analytics'],
    relatedProductIds: ['maintenance-mro', 'inventory-intelligence', 'windows-app'],
  },
  {
    id: 'electronics',
    name: 'Electronics',
    headline: 'Component sourcing and obsolescence management',
    summary:
      'Manage short-lifecycle SKUs, alternates, and supplier lead times for electronics manufacturing, repair depots, and capital equipment.',
    icon: 'memory',
    tags: ['electronics', 'components', 'obsolescence', 'alternates', 'bom', 'ems'],
    highlights: [
      'Dead-stock and obsolescence alerts (Solutions SYS-SOL-06)',
      'Template-driven attributes for manufacturer part numbers',
      'Cross-reference search across approved vendor lists',
    ],
    relatedSolutions: ['Procurement & Supply Chain', 'Data & Predictive Analytics'],
    relatedProductIds: ['ai-smart-search', 'technical-specs', 'procurement-optimization'],
  },
]

function matchesQuery(industry: Industry, query: string) {
  if (!query.trim()) return true
  const q = query.trim().toLowerCase()
  const haystack = [
    industry.name,
    industry.headline,
    industry.summary,
    ...industry.tags,
    ...industry.highlights,
    ...industry.relatedSolutions,
  ]
    .join(' ')
    .toLowerCase()
  return haystack.includes(q)
}

export function IndustriesPage() {
  const location = useLocation()
  const [query, setQuery] = useState('')

  useEffect(() => {
    if (!location.hash) return
    const id = location.hash.replace(/^#/, '')
    const el = document.getElementById(id)
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  }, [location.hash])

  const filtered = useMemo(
    () => INDUSTRIES.filter((industry) => matchesQuery(industry, query)),
    [query],
  )

  return (
    <MarketingFrame>
      <main className="w-full pt-20 bg-background min-h-[calc(100vh-5rem)]">
        <section className="max-w-[1360px] mx-auto px-gutter py-space-lg pb-space-xl">
          <div className="flex items-center gap-space-xs text-primary font-mono-data text-mono-data tracking-widest uppercase bg-surface-container px-space-sm py-1 rounded w-fit mb-space-md">
            <span className="inline-block w-2 h-2 rounded-full bg-primary animate-pulse" />
            <span>INDUSTRIES</span>
          </div>
          <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-space-md mb-space-lg">
            <div className="max-w-2xl">
              <h1 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight m-0">
                Built for your vertical
              </h1>
              <p className="font-body-md text-body-md text-on-surface-variant mt-space-sm m-0">
                PartFinder is industry-agnostic at the core (templates, parts, relations, search) but
                deployed with vertical workflows on the marketing site and in the Windows app setup
                profile — Marine, Aviation, and Plants on the home architect form; five verticals below
                match our public site navigation.
              </p>
            </div>
            <Link
              to="/#architect"
              className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors inline-flex items-center gap-1.5 shadow-sm"
            >
              Talk to an architect
              <Icon name="arrow_forward" className="text-[18px]" />
            </Link>
          </div>

          <div className="bg-surface-container-lowest rounded-xl p-space-md md:p-space-lg shadow-sm mb-space-lg">
            <div className="relative">
              <Icon
                name="search"
                className="text-[22px] text-outline absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
              />
              <input
                type="search"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Search industries (e.g. IMPA, FAA, plant MRO, OEM…)"
                className="w-full h-11 pl-10 pr-3 bg-surface-container-low rounded-lg font-body-sm text-body-sm text-on-surface placeholder:text-outline focus:outline-none focus:ring-2 focus:ring-primary border-0"
                aria-label="Search industries"
              />
            </div>
          </div>

          {filtered.length === 0 ? (
            <div className="bg-surface-container-low rounded-xl p-space-xl text-center">
              <p className="font-body-md text-body-md text-on-surface-variant m-0">
                No industries match your search. Try another keyword.
              </p>
            </div>
          ) : (
            <div className="flex flex-col gap-gutter-lg">
              {filtered.map((industry) => (
                <article
                  key={industry.id}
                  id={industry.id}
                  className="scroll-mt-28 bg-surface-container-lowest rounded-xl p-space-lg md:p-space-xl shadow-sm hover:shadow-md transition-shadow"
                >
                  <div className="flex flex-col md:flex-row md:items-start gap-space-md">
                    <div className="w-14 h-14 rounded-xl bg-surface-container-high flex items-center justify-center text-primary shrink-0">
                      <Icon name={industry.icon} className="text-[32px]" />
                    </div>
                    <div className="flex-grow min-w-0">
                      <h2 className="font-headline-md text-headline-md text-on-surface font-extrabold tracking-tight m-0">
                        {industry.name}
                      </h2>
                      <p className="font-label-md text-label-md text-primary mt-1 mb-space-sm m-0">
                        {industry.headline}
                      </p>
                      <p className="font-body-md text-body-md text-on-surface-variant m-0">{industry.summary}</p>
                      <ul className="mt-space-md pl-5 m-0 flex flex-col gap-2">
                        {industry.highlights.map((line) => (
                          <li key={line} className="font-body-sm text-body-sm text-on-surface">
                            {line}
                          </li>
                        ))}
                      </ul>
                      <div className="flex flex-wrap gap-1.5 mt-space-md">
                        {industry.tags.map((tag) => (
                          <span
                            key={tag}
                            className="font-mono-data text-caption bg-surface-container px-2 py-0.5 rounded-full text-on-surface font-semibold"
                          >
                            {tag}
                          </span>
                        ))}
                      </div>
                      <div className="mt-space-md flex flex-wrap gap-space-md">
                        <Link
                          to="/#solutions"
                          className="font-label-md text-label-md text-primary font-bold inline-flex items-center gap-1"
                        >
                          Related solutions
                          <Icon name="arrow_forward" className="text-[16px]" />
                        </Link>
                        <Link
                          to="/products"
                          className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface inline-flex items-center gap-1"
                        >
                          View products
                          <Icon name="open_in_new" className="text-[16px]" />
                        </Link>
                        <Link
                          to="/get-licence"
                          className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface inline-flex items-center gap-1"
                        >
                          Get Licence
                          <Icon name="payments" className="text-[16px]" />
                        </Link>
                      </div>
                    </div>
                  </div>
                </article>
              ))}
            </div>
          )}

          <p className="font-body-sm text-body-sm text-on-surface-variant mt-space-xl text-center m-0">
            Note: Backend org <strong>type</strong> is tier-based (standard / premium / enterprise), not
            industry — industries here describe how teams deploy PartFinder, not a separate database enum.
          </p>
        </section>
      </main>
    </MarketingFrame>
  )
}
