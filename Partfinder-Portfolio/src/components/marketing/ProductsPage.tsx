import { useEffect, useMemo, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { Icon, MarketingFrame, STORE_URL } from './SiteChrome'

type Product = {
  id: string
  name: string
  category: string
  summary: string
  icon: string
  tags: string[]
  bestFor: string
}

const PRODUCTS: Product[] = [
  {
    id: 'ai-smart-search',
    name: 'AI Smart Search',
    category: 'Platform',
    summary:
      'Natural-language and part-number search across OEM catalogues, cross-references, and your org inventory with ranked relevance.',
    icon: 'manage_search',
    tags: ['search', 'oem', 'cross-reference', 'nlp', 'catalogue'],
    bestFor: 'Procurement teams needing fast part identification',
  },
  {
    id: 'inventory-intelligence',
    name: 'Inventory Intelligence',
    category: 'Platform',
    summary:
      'Live stock levels, min/max alerts, and worksheet views so planners see what is on hand before raising an RFQ.',
    icon: 'inventory_2',
    tags: ['inventory', 'stock', 'alerts', 'warehouse', 'parts'],
    bestFor: 'Stores and MRO teams managing spare parts',
  },
  {
    id: 'supplier-network',
    name: 'Supplier Network',
    category: 'Platform',
    summary:
      'Route RFQs to approved vendors, compare lead times, and track responses in one pipeline tied to your templates.',
    icon: 'hub',
    tags: ['suppliers', 'rfq', 'vendors', 'procurement', 'sourcing'],
    bestFor: 'Buyers coordinating multiple suppliers',
  },
  {
    id: 'technical-specs',
    name: 'Technical Specs',
    category: 'Platform',
    summary:
      'Structured master data, templates, and relations so every part carries the attributes your industry requires.',
    icon: 'fact_check',
    tags: ['specs', 'master data', 'templates', 'attributes', 'compliance'],
    bestFor: 'Engineering and quality teams standardizing data',
  },
  {
    id: 'api-integrations',
    name: 'API & Integrations',
    category: 'Platform',
    summary:
      'Connect PartFinder to ERP, CMMS, and custom systems via APIs and import flows for templates and parts.',
    icon: 'api',
    tags: ['api', 'integration', 'erp', 'sap', 'maximo', 'import'],
    bestFor: 'IT teams wiring PartFinder into existing stacks',
  },
  {
    id: 'windows-app',
    name: 'PartFinder Windows App',
    category: 'Client',
    summary:
      'Licenced desktop client for daily search, worksheets, master data, and org setup — the primary workspace for operators.',
    icon: 'desktop_windows',
    tags: ['windows', 'desktop', 'app', 'client', 'download'],
    bestFor: 'Day-to-day users in the plant, yard, or office',
  },
  {
    id: 'fleet-vessel',
    name: 'Fleet & Vessel',
    category: 'Solution module',
    summary:
      'IMPA/ISSA-aware workflows for marine fleets, drydock, and bonded logistics with voyage-centric part tracking.',
    icon: 'directions_boat',
    tags: ['marine', 'fleet', 'vessel', 'impa', 'issa', 'shipping'],
    bestFor: 'Naval and global shipping operations',
  },
  {
    id: 'maintenance-mro',
    name: 'Maintenance & MRO',
    category: 'Solution module',
    summary:
      'PM kitting, barcode/OCR-friendly field use, and downtime triage aligned with plant maintenance processes.',
    icon: 'build',
    tags: ['mro', 'maintenance', 'pm', 'plant', 'kitting', 'barcode'],
    bestFor: 'Manufacturing and heavy industrial MRO',
  },
  {
    id: 'procurement-optimization',
    name: 'Procurement Optimisation',
    category: 'Solution module',
    summary:
      'Shorten RFQ cycles with automated sourcing rules, preferred vendors, and spend visibility across categories.',
    icon: 'shopping_cart',
    tags: ['procurement', 'rfq', 'sourcing', 'cost', 'optimisation'],
    bestFor: 'Central procurement and shared services',
  },
]

const CATEGORIES = ['All', ...Array.from(new Set(PRODUCTS.map((p) => p.category)))]

function matchesQuery(product: Product, query: string) {
  if (!query.trim()) return true
  const q = query.trim().toLowerCase()
  const haystack = [
    product.name,
    product.category,
    product.summary,
    product.bestFor,
    ...product.tags,
  ]
    .join(' ')
    .toLowerCase()
  return haystack.includes(q)
}

export function ProductsPage() {
  const location = useLocation()
  const [query, setQuery] = useState('')
  const [category, setCategory] = useState('All')

  useEffect(() => {
    if (!location.hash) return
    const id = location.hash.replace(/^#/, '')
    const el = document.getElementById(id)
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' })
    }
  }, [location.hash])

  const filtered = useMemo(() => {
    return PRODUCTS.filter((product) => {
      if (category !== 'All' && product.category !== category) return false
      return matchesQuery(product, query)
    })
  }, [query, category])

  return (
    <MarketingFrame>
      <main className="w-full pt-20 bg-background min-h-[calc(100vh-5rem)]">
        <section className="max-w-[1360px] mx-auto px-gutter py-space-lg pb-space-xl">
          <div className="flex items-center gap-space-xs text-primary font-mono-data text-mono-data tracking-widest uppercase bg-surface-container px-space-sm py-1 rounded w-fit mb-space-md">
            <span className="inline-block w-2 h-2 rounded-full bg-primary animate-pulse" />
            <span>PRODUCTS</span>
          </div>
          <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-space-md mb-space-lg">
            <div className="max-w-2xl">
              <h1 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight m-0">
                PartFinder products
              </h1>
              <p className="font-body-md text-body-md text-on-surface-variant mt-space-sm m-0">
                Browse platform capabilities, client apps, and industry modules. Search by keyword, tag, or
                use case to find what fits your operation.
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <Link
                to="/get-licence"
                className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors inline-flex items-center gap-1.5 shadow-sm"
              >
                Get Licence
                <Icon name="arrow_forward" className="text-[18px]" />
              </Link>
              <a
                href={STORE_URL}
                target="_blank"
                rel="noopener noreferrer"
                className="bg-surface-container-high hover:bg-surface-container-highest text-on-surface font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors inline-flex items-center gap-1.5"
              >
                <Icon name="download" className="text-[18px]" />
                Download App
              </a>
            </div>
          </div>

          <div className="bg-surface-container-lowest rounded-xl p-space-md md:p-space-lg shadow-sm mb-space-lg flex flex-col md:flex-row gap-space-md">
            <div className="flex-grow relative">
              <Icon
                name="search"
                className="text-[22px] text-outline absolute left-3 top-1/2 -translate-y-1/2 pointer-events-none"
              />
              <input
                type="search"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Search products (e.g. RFQ, marine, inventory, API…)"
                className="w-full h-11 pl-10 pr-3 bg-surface-container-low rounded-lg font-body-sm text-body-sm text-on-surface placeholder:text-outline focus:outline-none focus:ring-2 focus:ring-primary border-0"
                aria-label="Search products"
              />
            </div>
            <div className="flex flex-wrap gap-2 items-center">
              {CATEGORIES.map((cat) => (
                <button
                  key={cat}
                  type="button"
                  onClick={() => setCategory(cat)}
                  className={
                    category === cat
                      ? 'bg-primary text-on-primary font-label-sm text-label-sm px-3 py-1.5 rounded-full border-0'
                      : 'bg-surface-container-low text-on-surface-variant hover:text-on-surface font-label-sm text-label-sm px-3 py-1.5 rounded-full border-0'
                  }
                >
                  {cat}
                </button>
              ))}
            </div>
          </div>

          {filtered.length === 0 ? (
            <div className="bg-surface-container-low rounded-xl p-space-xl text-center">
              <p className="font-body-md text-body-md text-on-surface-variant m-0">
                No products match your search. Try a different keyword or choose <strong>All</strong>.
              </p>
              <Link to="/#architect" className="font-label-md text-label-md text-primary font-bold mt-space-md inline-block">
                Talk to us about a custom requirement
              </Link>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-gutter-lg">
              {filtered.map((product) => (
                <article
                  key={product.id}
                  id={product.id}
                  className="scroll-mt-28 bg-surface-container-lowest rounded-xl p-space-lg shadow-sm hover:shadow-md transition-shadow flex flex-col gap-space-sm"
                >
                  <div className="flex items-start gap-3">
                    <div className="w-11 h-11 rounded-lg bg-surface-container-high flex items-center justify-center text-primary shrink-0">
                      <Icon name={product.icon} className="text-[26px]" />
                    </div>
                    <div className="min-w-0">
                      <span className="font-caption text-caption text-primary uppercase font-bold tracking-wider">
                        {product.category}
                      </span>
                      <h2 className="font-headline-sm text-headline-sm text-on-surface font-extrabold tracking-tight m-0 mt-0.5">
                        {product.name}
                      </h2>
                    </div>
                  </div>
                  <p className="font-body-sm text-body-sm text-on-surface-variant m-0 flex-grow">{product.summary}</p>
                  <p className="font-caption text-caption text-outline m-0">
                    <span className="font-semibold text-on-surface-variant">Best for:</span> {product.bestFor}
                  </p>
                  <div className="flex flex-wrap gap-1.5 pt-1">
                    {product.tags.slice(0, 4).map((tag) => (
                      <span
                        key={tag}
                        className="font-mono-data text-caption bg-surface-container px-2 py-0.5 rounded-full text-on-surface font-semibold"
                      >
                        {tag}
                      </span>
                    ))}
                  </div>
                  {product.id === 'windows-app' ? (
                    <a
                      href={STORE_URL}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="font-label-md text-label-md text-primary font-bold inline-flex items-center gap-1 mt-space-xs"
                    >
                      Get the app
                      <Icon name="open_in_new" className="text-[16px]" />
                    </a>
                  ) : (
                    <Link
                      to="/get-licence"
                      className="font-label-md text-label-md text-primary font-bold inline-flex items-center gap-1 mt-space-xs"
                    >
                      Start with a licence
                      <Icon name="arrow_forward" className="text-[16px]" />
                    </Link>
                  )}
                </article>
              ))}
            </div>
          )}

          <p className="font-body-sm text-body-sm text-on-surface-variant mt-space-xl text-center m-0">
            Need industry-specific deployment detail?{' '}
            <Link to="/#solutions" className="text-primary font-bold">
              View solutions on the home page
            </Link>
            .
          </p>
        </section>
      </main>
    </MarketingFrame>
  )
}
