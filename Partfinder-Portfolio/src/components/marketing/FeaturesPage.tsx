import { useEffect, useMemo, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { Icon, MarketingFrame, STORE_URL } from './SiteChrome'

export type FeatureId =
  | 'dashboard'
  | 'inventory'
  | 'master-data'
  | 'parts-search'
  | 'templates'
  | 'bulk-import'
  | 'csv-import'
  | 'worksheet-relations'
  | 'view-data'
  | 'qr-codes'
  | 'alerts'
  | 'audit'
  | 'user-management'
  | 'rbac'
  | 'licensing'
  | 'setup-wizard'
  | 'tenant-database'
  | 'maintenance-mode'
  | 'api-access'

type Feature = {
  id: FeatureId
  name: string
  category: 'Windows app' | 'Data & templates' | 'Search & parts' | 'Operations' | 'Security & admin' | 'Platform'
  summary: string
  icon: string
  tags: string[]
  inApp: boolean
  inBackend: boolean
}

const FEATURES: Feature[] = [
  {
    id: 'dashboard',
    name: 'Operational dashboard',
    category: 'Windows app',
    summary:
      'Home view with KPIs and shortcuts into templates, master data, relations, and QR tools — backed by dashboard stats from the API when online.',
    icon: 'dashboard',
    tags: ['dashboard', 'kpi', 'home', 'analytics'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'inventory',
    name: 'Inventory',
    category: 'Windows app',
    summary:
      'Inventory workspace tied to your template schema so stores teams see stock-oriented views alongside structured part data.',
    icon: 'inventory_2',
    tags: ['inventory', 'stock', 'warehouse'],
    inApp: true,
    inBackend: false,
  },
  {
    id: 'master-data',
    name: 'Master Data explorer',
    category: 'Data & templates',
    summary:
      'Spreadsheet-style master data grid with add/edit/copy/delete permissions per role — the canonical worksheet for your org catalogue.',
    icon: 'table',
    tags: ['master data', 'grid', 'worksheet', 'catalogue'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'templates',
    name: 'Template designer',
    category: 'Data & templates',
    summary:
      'Define templates with custom columns and rows; control who can add, view, edit, or delete templates per user profile.',
    icon: 'view_column',
    tags: ['templates', 'columns', 'schema', 'fields'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'bulk-import',
    name: 'Bulk template import',
    category: 'Data & templates',
    summary:
      'Import many templates from Excel or CSV (including BulkTemplates workbook layout) without rebuilding schemas manually in the client.',
    icon: 'upload_file',
    tags: ['excel', 'xlsx', 'bulk', 'onboarding'],
    inApp: true,
    inBackend: false,
  },
  {
    id: 'csv-import',
    name: 'CSV row import',
    category: 'Data & templates',
    summary:
      'Upload CSV against a template via the API with header mapping and async job status — used for large data loads and integrations.',
    icon: 'table_rows',
    tags: ['csv', 'import', 'api', 'integration'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'parts-search',
    name: 'Parts registry & search',
    category: 'Search & parts',
    summary:
      'Org-scoped parts with metadata, list/filter by template, low-stock views, and Elasticsearch-backed search when the cluster is available.',
    icon: 'manage_search',
    tags: ['parts', 'search', 'elasticsearch', 'low stock'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'worksheet-relations',
    name: 'Link Templates (relations)',
    category: 'Data & templates',
    summary:
      'Define worksheet relations between templates so match keys connect rows across catalogues (Bill of materials, cross-reference sheets).',
    icon: 'link',
    tags: ['relations', 'link templates', 'match keys', 'bom'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'view-data',
    name: 'Enriched view data',
    category: 'Search & parts',
    summary:
      'API endpoint resolves a primary template’s rows enriched with related template data — powers combined views in the client.',
    icon: 'table_view',
    tags: ['view data', 'join', 'enriched rows'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'qr-codes',
    name: 'QR code manager',
    category: 'Operations',
    summary:
      'Generate and manage QR codes linked to template rows for fast floor or field identification.',
    icon: 'qr_code_2',
    tags: ['qr', 'barcode', 'field', 'scan'],
    inApp: true,
    inBackend: false,
  },
  {
    id: 'alerts',
    name: 'Alerts',
    category: 'Operations',
    summary:
      'Operational alerts stored per org so teams see exceptions and follow-ups without leaving the desktop shell.',
    icon: 'notifications',
    tags: ['alerts', 'notifications'],
    inApp: true,
    inBackend: false,
  },
  {
    id: 'audit',
    name: 'Activities (audit)',
    category: 'Operations',
    summary:
      'Activity history for accountability — review who changed what across templates and master data workflows.',
    icon: 'history',
    tags: ['audit', 'activities', 'compliance'],
    inApp: true,
    inBackend: false,
  },
  {
    id: 'user-management',
    name: 'User management & invites',
    category: 'Security & admin',
    summary:
      'Invite org users with email, org code, and temporary credentials; first admin setup via the public setup API.',
    icon: 'group',
    tags: ['users', 'invite', 'admin', 'setup'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'rbac',
    name: 'Role-based access',
    category: 'Security & admin',
    summary:
      'Fine-grained capabilities for templates and master data (view/edit/delete, template scoping on Parts). Casbin on the backend supports policy enforcement.',
    icon: 'admin_panel_settings',
    tags: ['rbac', 'permissions', 'casbin', 'roles'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'licensing',
    name: 'Organisation licensing',
    category: 'Platform',
    summary:
      'Six-digit org code acts as the licence key for the Windows app; validity, ban, and maintenance checks gate access.',
    icon: 'verified_user',
    tags: ['licence', 'org code', 'subscription'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'setup-wizard',
    name: 'Setup wizard',
    category: 'Platform',
    summary:
      'Provision a default tenant database from the cluster pool or connect a custom MongoDB URI; test connection before go-live.',
    icon: 'settings_account_box',
    tags: ['setup', 'database', 'onboarding'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'tenant-database',
    name: 'Per-org tenant data',
    category: 'Platform',
    summary:
      'Each organisation can have an isolated MongoDB for templates, users, and setup metadata — platform DB holds org registry only.',
    icon: 'database',
    tags: ['multi-tenant', 'mongodb', 'isolation'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'maintenance-mode',
    name: 'Platform maintenance',
    category: 'Platform',
    summary:
      'Scheduled maintenance window exposed publicly so clients show countdown and block usage until the window ends.',
    icon: 'construction',
    tags: ['maintenance', 'status'],
    inApp: true,
    inBackend: true,
  },
  {
    id: 'api-access',
    name: 'HTTP API',
    category: 'Platform',
    summary:
      'NestJS REST API under /api with JWT bearer tokens and X-Org-Id header for tenant routes — same contract the Windows app uses.',
    icon: 'api',
    tags: ['api', 'rest', 'integration'],
    inApp: true,
    inBackend: true,
  },
]

const CATEGORIES = ['All', ...Array.from(new Set(FEATURES.map((f) => f.category)))]

function matchesQuery(feature: Feature, query: string) {
  if (!query.trim()) return true
  const q = query.trim().toLowerCase()
  return [feature.name, feature.category, feature.summary, ...feature.tags].join(' ').toLowerCase().includes(q)
}

export function FeaturesPage() {
  const location = useLocation()
  const [query, setQuery] = useState('')
  const [category, setCategory] = useState('All')

  useEffect(() => {
    if (!location.hash) return
    const el = document.getElementById(location.hash.replace(/^#/, ''))
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }, [location.hash])

  const filtered = useMemo(() => {
    return FEATURES.filter((feature) => {
      if (category !== 'All' && feature.category !== category) return false
      return matchesQuery(feature, query)
    })
  }, [query, category])

  return (
    <MarketingFrame>
      <main className="w-full pt-20 bg-background min-h-[calc(100vh-5rem)]">
        <section className="max-w-[1360px] mx-auto px-gutter py-space-lg pb-space-xl">
          <div className="flex items-center gap-space-xs text-primary font-mono-data text-mono-data tracking-widest uppercase bg-surface-container px-space-sm py-1 rounded w-fit mb-space-md">
            <span className="inline-block w-2 h-2 rounded-full bg-primary animate-pulse" />
            <span>FEATURES</span>
          </div>
          <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-space-md mb-space-lg">
            <div className="max-w-2xl">
              <h1 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight m-0">
                PartFinder features
              </h1>
              <p className="font-body-md text-body-md text-on-surface-variant mt-space-sm m-0">
                Capabilities shipped in the{' '}
                <strong className="text-on-surface">Windows app</strong> and{' '}
                <strong className="text-on-surface">backend API</strong> today — not marketing-only bullets.
                Search or filter by area to see what your teams get after licence setup.
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <a
                href={STORE_URL}
                target="_blank"
                rel="noopener noreferrer"
                className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors inline-flex items-center gap-1.5 shadow-sm"
              >
                <Icon name="download" className="text-[18px]" />
                Download app
              </a>
              <Link
                to="/#impact"
                className="bg-surface-container-high hover:bg-surface-container-highest text-on-surface font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors inline-flex items-center gap-1.5"
              >
                Impact metrics
                <Icon name="trending_up" className="text-[18px]" />
              </Link>
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
                placeholder="Search features (templates, QR, licence, import…)"
                className="w-full h-11 pl-10 pr-3 bg-surface-container-low rounded-lg font-body-sm text-body-sm text-on-surface placeholder:text-outline focus:outline-none focus:ring-2 focus:ring-primary border-0"
                aria-label="Search features"
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

          <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-gutter-lg">
            {filtered.map((feature) => (
              <article
                key={feature.id}
                id={feature.id}
                className="scroll-mt-28 bg-surface-container-lowest rounded-xl p-space-lg shadow-sm hover:shadow-md transition-shadow flex flex-col gap-space-sm"
              >
                <div className="flex items-start gap-3">
                  <div className="w-11 h-11 rounded-lg bg-surface-container-high flex items-center justify-center text-primary shrink-0">
                    <Icon name={feature.icon} className="text-[26px]" />
                  </div>
                  <div className="min-w-0">
                    <span className="font-caption text-caption text-primary uppercase font-bold tracking-wider">
                      {feature.category}
                    </span>
                    <h2 className="font-headline-sm text-headline-sm text-on-surface font-extrabold tracking-tight m-0 mt-0.5">
                      {feature.name}
                    </h2>
                  </div>
                </div>
                <p className="font-body-sm text-body-sm text-on-surface-variant m-0 flex-grow">{feature.summary}</p>
                <div className="flex gap-2 font-caption text-caption">
                  {feature.inApp ? (
                    <span className="bg-secondary/10 text-secondary px-2 py-0.5 rounded font-semibold">Windows app</span>
                  ) : null}
                  {feature.inBackend ? (
                    <span className="bg-primary/10 text-primary px-2 py-0.5 rounded font-semibold">API</span>
                  ) : null}
                </div>
                <div className="flex flex-wrap gap-1.5 pt-1">
                  {feature.tags.slice(0, 4).map((tag) => (
                    <span
                      key={tag}
                      className="font-mono-data text-caption bg-surface-container px-2 py-0.5 rounded-full text-on-surface font-semibold"
                    >
                      {tag}
                    </span>
                  ))}
                </div>
              </article>
            ))}
          </div>

          {filtered.length === 0 ? (
            <p className="font-body-md text-body-md text-on-surface-variant text-center mt-space-lg m-0">
              No features match. Reset filters or try another keyword.
            </p>
          ) : null}

          <p className="font-body-sm text-body-sm text-on-surface-variant mt-space-xl text-center m-0">
            Explore how features map to packaging on{' '}
            <Link to="/products" className="text-primary font-bold">
              Products
            </Link>{' '}
            or verticals on{' '}
            <Link to="/industries" className="text-primary font-bold">
              Industries
            </Link>
            .
          </p>
        </section>
      </main>
    </MarketingFrame>
  )
}
