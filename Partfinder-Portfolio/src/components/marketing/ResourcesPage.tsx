import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { buildApiUrl, setupApiBase } from './accountSession'
import { Icon, MarketingFrame, STORE_URL } from './SiteChrome'

type ResourceLink = {
  id: string
  title: string
  description: string
  icon: string
  category: 'Get started' | 'Downloads' | 'Guides' | 'Reference' | 'Support'
  href?: string
  to?: string
  external?: boolean
}

const RESOURCES: ResourceLink[] = [
  {
    id: 'download-app',
    title: 'Download Windows app',
    description:
      'Official PartFinder client on Microsoft Store — same link used in invite emails and backend INVITE_DOWNLOAD_LINK.',
    icon: 'download',
    category: 'Downloads',
    href: STORE_URL,
    external: true,
  },
  {
    id: 'get-licence',
    title: 'Get Licence (self-serve)',
    description: 'Purchase a Starter licence online and receive org code + temporary admin password.',
    icon: 'payments',
    category: 'Get started',
    to: '/get-licence',
  },
  {
    id: 'sign-in',
    title: 'Sign in (org account)',
    description: 'Web sign-in using the same credentials as the Windows app (invite-login API).',
    icon: 'login',
    category: 'Get started',
    to: '/sign-in',
  },
  {
    id: 'pricing',
    title: 'Pricing & subscription plans',
    description: 'Plans enforced by licence verification: demo, trial, starter, professional, annual, lifetime.',
    icon: 'sell',
    category: 'Get started',
    to: '/pricing',
  },
  {
    id: 'features',
    title: 'Feature catalogue',
    description: 'Windows app modules and backend APIs that ship today (templates, parts search, setup, RBAC).',
    icon: 'apps',
    category: 'Guides',
    to: '/features',
  },
  {
    id: 'products',
    title: 'Products overview',
    description: 'How platform capabilities are packaged for procurement, inventory, and integrations.',
    icon: 'inventory_2',
    category: 'Guides',
    to: '/products',
  },
  {
    id: 'industries-impa',
    title: 'IMPA / ISSA & marine vertical',
    description: 'Marine deployment patterns referenced in Solutions and the Industries page.',
    icon: 'directions_boat',
    category: 'Guides',
    to: '/industries#marine',
  },
  {
    id: 'case-study',
    title: 'Case study & impact metrics',
    description: 'TransOcean fleet example and verified enterprise metrics on the home page.',
    icon: 'trending_up',
    category: 'Guides',
    to: '/#impact',
  },
  {
    id: 'architect',
    title: 'Contact solutions architects',
    description: 'Enterprise advisory form — ERP/CMMS roadmap, taxonomy audit, on-site consultation.',
    icon: 'engineering',
    category: 'Support',
    to: '/#architect',
  },
  {
    id: 'bulk-import',
    title: 'Bulk template import (Excel/CSV)',
    description:
      'In the Windows app: use a BulkTemplates sheet (TemplateName, Columns) or per-sheet layouts documented in BulkTemplateImportService.',
    icon: 'upload_file',
    category: 'Reference',
    to: '/resources#bulk-import',
  },
  {
    id: 'standard-columns',
    title: 'Standard column catalogue',
    description:
      'Built-in column labels (Brand, Model, Part No, Vendor, etc.) from PartFinder.Core ColumnCatalog for templates.',
    icon: 'view_column',
    category: 'Reference',
    to: '/resources#standard-columns',
  },
  {
    id: 'api',
    title: 'HTTP API (same as desktop client)',
    description:
      'NestJS /api routes with JWT + X-Org-Id for tenant data; public setup and licence endpoints for onboarding.',
    icon: 'api',
    category: 'Reference',
    to: '/resources#api',
  },
]

const STANDARD_COLUMNS = [
  { label: 'Brand', category: 'Vehicle' },
  { label: 'Model', category: 'Vehicle' },
  { label: 'Category', category: 'Vehicle' },
  { label: 'Part No', category: 'Part identity' },
  { label: 'Position No', category: 'Part identity' },
  { label: 'Vendor', category: 'Vendor' },
  { label: 'Vendor Warehouse', category: 'Vendor' },
  { label: 'Vendor Phone Number', category: 'Vendor' },
  { label: 'Type', category: 'Vendor' },
]

const RELEASE_HIGHLIGHTS = [
  'Microsoft Store download link in invite emails (legacy URLs remapped).',
  'Optional TOTP two-factor gate at login when enabled on the device.',
  'Faster dashboard load via parallel API calls and shell warm-up.',
  'Admin invites receive full permissions and all-template access.',
]

type MaintenanceState =
  | { status: 'loading' }
  | { status: 'ok'; active: boolean; maintenanceUntil?: string }
  | { status: 'error' }

function ResourceCard({ item }: { item: ResourceLink }) {
  const className =
    'flex flex-col gap-2 rounded-xl bg-surface-container-lowest p-space-md shadow-sm hover:shadow-md transition-shadow h-full'
  const inner = (
    <>
      <div className="flex items-center gap-2">
        <Icon name={item.icon} className="text-[22px] text-primary" />
        <h3 className="font-label-md text-label-md text-on-surface font-bold m-0">{item.title}</h3>
      </div>
      <p className="font-body-sm text-body-sm text-on-surface-variant m-0 flex-grow">{item.description}</p>
      <span className="font-label-sm text-label-sm text-primary font-bold inline-flex items-center gap-1">
        Open
        <Icon name={item.external ? 'open_in_new' : 'arrow_forward'} className="text-[16px]" />
      </span>
    </>
  )

  if (item.external && item.href) {
    return (
      <a className={className} href={item.href} target="_blank" rel="noopener noreferrer">
        {inner}
      </a>
    )
  }
  if (item.to) {
    return (
      <Link className={className} to={item.to}>
        {inner}
      </Link>
    )
  }
  return <div className={className}>{inner}</div>
}

function PlatformStatusPanel() {
  const [state, setState] = useState<MaintenanceState>({ status: 'loading' })

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const response = await fetch(buildApiUrl('/public/platform/maintenance'))
        const data = (await response.json().catch(() => null)) as {
          active?: boolean
          maintenanceUntil?: string
        } | null
        if (cancelled) return
        if (!response.ok || !data) {
          setState({ status: 'error' })
          return
        }
        setState({
          status: 'ok',
          active: Boolean(data.active),
          maintenanceUntil: data.maintenanceUntil,
        })
      } catch {
        if (!cancelled) setState({ status: 'error' })
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  return (
    <div id="system-status" className="scroll-mt-28 bg-surface-container-low rounded-xl p-space-lg shadow-sm">
      <h2 className="font-headline-sm text-headline-sm text-on-surface font-extrabold m-0 flex items-center gap-2">
        <Icon name="cloud" className="text-[24px] text-primary" />
        System status
      </h2>
      <p className="font-body-sm text-body-sm text-on-surface-variant mt-2 m-0">
        Live signal from <code className="font-mono-data text-caption">GET /api/public/platform/maintenance</code>{' '}
        — same check the Windows app uses during licence verification.
      </p>
      <div className="mt-space-md">
        {state.status === 'loading' ? (
          <p className="font-body-sm text-body-sm text-on-surface-variant m-0">Checking platform status…</p>
        ) : null}
        {state.status === 'error' ? (
          <p className="font-body-sm text-body-sm text-on-surface-variant m-0">
            Could not reach the API. If you are developing locally, start PartFinder-Backend or set{' '}
            <code className="font-mono-data text-caption">VITE_API_URL</code>.
          </p>
        ) : null}
        {state.status === 'ok' && !state.active ? (
          <p className="font-body-sm text-body-sm text-secondary m-0 flex items-center gap-2">
            <Icon name="check_circle" className="text-[20px]" />
            All systems operational — no scheduled maintenance window.
          </p>
        ) : null}
        {state.status === 'ok' && state.active ? (
          <p className="font-body-sm text-body-sm text-error m-0 flex items-start gap-2">
            <Icon name="construction" className="text-[20px]" />
            <span>
              Scheduled maintenance is active.
              {state.maintenanceUntil
                ? ` Expected end: ${new Date(state.maintenanceUntil).toLocaleString()}.`
                : null}
            </span>
          </p>
        ) : null}
      </div>
    </div>
  )
}

export function ResourcesPage() {
  const [query, setQuery] = useState('')
  const categories = useMemo(
    () => ['All', ...Array.from(new Set(RESOURCES.map((r) => r.category)))],
    [],
  )
  const [category, setCategory] = useState('All')

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase()
    return RESOURCES.filter((item) => {
      if (category !== 'All' && item.category !== category) return false
      if (!q) return true
      return [item.title, item.description, item.category].join(' ').toLowerCase().includes(q)
    })
  }, [query, category])

  const apiBase = setupApiBase() || '(same origin /api in dev)'

  return (
    <MarketingFrame>
      <main className="w-full pt-20 bg-background min-h-[calc(100vh-5rem)]">
        <section className="max-w-[1360px] mx-auto px-gutter py-space-lg pb-space-xl">
          <div className="flex items-center gap-space-xs text-primary font-mono-data text-mono-data tracking-widest uppercase bg-surface-container px-space-sm py-1 rounded w-fit mb-space-md">
            <span className="inline-block w-2 h-2 rounded-full bg-primary animate-pulse" />
            <span>RESOURCES</span>
          </div>
          <h1 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight m-0 max-w-3xl">
            PartFinder resources
          </h1>
          <p className="font-body-md text-body-md text-on-surface-variant mt-space-sm max-w-3xl m-0">
            Downloads, onboarding, guides, and reference material tied to the real app and API — not placeholder
            footer links.
          </p>

          <div className="bg-surface-container-lowest rounded-xl p-space-md shadow-sm mt-space-lg mb-space-lg">
            <input
              type="search"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Search resources…"
              className="w-full h-11 px-3 bg-surface-container-low rounded-lg font-body-sm text-body-sm text-on-surface placeholder:text-outline focus:outline-none focus:ring-2 focus:ring-primary border-0 mb-3"
              aria-label="Search resources"
            />
            <div className="flex flex-wrap gap-2">
              {categories.map((cat) => (
                <button
                  key={cat}
                  type="button"
                  onClick={() => setCategory(cat)}
                  className={
                    category === cat
                      ? 'bg-primary text-on-primary font-label-sm text-label-sm px-3 py-1.5 rounded-full border-0'
                      : 'bg-surface-container-low text-on-surface-variant font-label-sm text-label-sm px-3 py-1.5 rounded-full border-0'
                  }
                >
                  {cat}
                </button>
              ))}
            </div>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-gutter-lg mb-space-xl">
            {filtered.map((item) => (
              <ResourceCard key={item.id} item={item} />
            ))}
          </div>

          <div className="flex flex-col gap-gutter-lg mb-space-xl">
            <PlatformStatusPanel />

            <div id="release-notes" className="scroll-mt-28 bg-surface-container-lowest rounded-xl p-space-lg shadow-sm">
              <h2 className="font-headline-sm text-headline-sm text-on-surface font-extrabold m-0">
                Release highlights (3.0.5.0)
              </h2>
              <p className="font-body-sm text-body-sm text-on-surface-variant mt-2 m-0">
                From PartFinder Windows app release notes in the repository.
              </p>
              <ul className="mt-space-md pl-5 m-0 flex flex-col gap-2">
                {RELEASE_HIGHLIGHTS.map((line) => (
                  <li key={line} className="font-body-sm text-body-sm text-on-surface">
                    {line}
                  </li>
                ))}
              </ul>
            </div>

            <div id="bulk-import" className="scroll-mt-28 bg-surface-container-lowest rounded-xl p-space-lg shadow-sm">
              <h2 className="font-headline-sm text-headline-sm text-on-surface font-extrabold m-0">
                Bulk template import
              </h2>
              <p className="font-body-sm text-body-sm text-on-surface-variant mt-2 m-0">
                Windows app: Excel workbook with a sheet named <strong>BulkTemplates</strong> (columns TemplateName and
                Columns), or CSV with the same header. Per-template sheets can also define column labels on row 1 with
                data below (file name becomes template name). Server-side CSV row import uses{' '}
                <code className="font-mono-data text-caption">POST /api/templates/:id/import</code>.
              </p>
            </div>

            <div id="standard-columns" className="scroll-mt-28 bg-surface-container-lowest rounded-xl p-space-lg shadow-sm">
              <h2 className="font-headline-sm text-headline-sm text-on-surface font-extrabold m-0">
                Standard column catalogue
              </h2>
              <p className="font-body-sm text-body-sm text-on-surface-variant mt-2 m-0">
                Add standard columns from Templates in the app — labels below come from{' '}
                <code className="font-mono-data text-caption">PartFinder.Core.ColumnCatalog</code>.
              </p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 mt-space-md">
                {STANDARD_COLUMNS.map((col) => (
                  <div key={col.label} className="bg-surface-container-low rounded-lg px-3 py-2 flex justify-between gap-2">
                    <span className="font-body-sm text-body-sm text-on-surface">{col.label}</span>
                    <span className="font-caption text-caption text-outline">{col.category}</span>
                  </div>
                ))}
              </div>
            </div>

            <div id="api" className="scroll-mt-28 bg-surface-container-lowest rounded-xl p-space-lg shadow-sm">
              <h2 className="font-headline-sm text-headline-sm text-on-surface font-extrabold m-0">
                API base URL
              </h2>
              <p className="font-body-sm text-body-sm text-on-surface-variant mt-2 m-0">
                Portfolio and dev proxy use:{' '}
                <code className="font-mono-data text-caption break-all">{apiBase}</code>
              </p>
              <p className="font-body-sm text-body-sm text-on-surface-variant mt-2 m-0">
                Public onboarding: <code className="font-mono-data text-caption">/api/public/setup/*</code>,{' '}
                <code className="font-mono-data text-caption">/api/public/license</code>,{' '}
                <code className="font-mono-data text-caption">/api/public/license-purchase/*</code>.
              </p>
            </div>
          </div>
        </section>
      </main>
    </MarketingFrame>
  )
}
