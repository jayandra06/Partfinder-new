import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { clearAccountSession, readAccountSession } from './accountSession'
import type { AccountSession } from './accountSession'

export const LOGO_SRC = '/partfinder-logo.png'

export const STORE_URL =
  'https://apps.microsoft.com/detail/9N6XVKPX189W?hl=en-us&gl=IN&ocid=pdpshare'

type NavItem = {
  label: string
  href: string
  productsPage?: boolean
  industriesPage?: boolean
  featuresPage?: boolean
  pricingPage?: boolean
  resourcesPage?: boolean
  homeCurrent?: boolean
}

const NAV: NavItem[] = [
  { label: 'Products', href: '/products', productsPage: true },
  { label: 'Solutions', href: '/#solutions', homeCurrent: true },
  { label: 'Industries', href: '/industries', industriesPage: true },
  { label: 'Features', href: '/features', featuresPage: true },
  { label: 'Pricing', href: '/pricing', pricingPage: true },
  { label: 'Resources', href: '/resources', resourcesPage: true },
  { label: 'About', href: '/#impact' },
  { label: 'Contact', href: '/#architect' },
]

function isNavItemActive(item: NavItem, pathname: string, onHome: boolean) {
  if (item.productsPage) {
    return pathname === '/products'
  }
  if (item.industriesPage) {
    return pathname === '/industries'
  }
  if (item.featuresPage) {
    return pathname === '/features'
  }
  if (item.pricingPage) {
    return pathname === '/pricing'
  }
  if (item.resourcesPage) {
    return pathname === '/resources'
  }
  if (item.homeCurrent) {
    return onHome && pathname === '/'
  }
  return false
}

function navLinkClass(active: boolean) {
  return active
    ? 'transition-colors text-primary font-label-md font-bold whitespace-nowrap'
    : 'font-label-md text-label-md text-on-surface-variant hover:text-on-surface transition-colors whitespace-nowrap'
}

export function Icon({
  name,
  className = '',
  fill = false,
}: {
  name: string
  className?: string
  fill?: boolean
}) {
  return (
    <span
      className={`material-symbols-outlined ${className}`}
      style={fill ? { fontVariationSettings: "'FILL' 1" } : undefined}
      aria-hidden="true"
    >
      {name}
    </span>
  )
}

function DownloadLink({ className = '' }: { className?: string }) {
  return (
    <a
      className={`inline-flex items-center gap-1.5 bg-surface-container text-primary font-label-md text-label-md px-space-md py-space-sm rounded-lg hover:bg-surface-container-high transition-colors font-semibold whitespace-nowrap ${className}`}
      href={STORE_URL}
      target="_blank"
      rel="noopener noreferrer"
    >
      <Icon name="download" className="text-[18px]" />
      <span>Download App</span>
    </a>
  )
}

export function SiteHeader() {
  const location = useLocation()
  const [account, setAccount] = useState<AccountSession | null>(() => readAccountSession())
  const [mobileNavOpen, setMobileNavOpen] = useState(false)
  const onHome = location.pathname === '/'
  const onLicence = location.pathname === '/get-licence'
  const onSignIn = location.pathname === '/sign-in'
  const onProducts = location.pathname === '/products'
  const onIndustries = location.pathname === '/industries'

  useEffect(() => {
    setAccount(readAccountSession())
  }, [location.pathname])

  useEffect(() => {
    setMobileNavOpen(false)
  }, [location.pathname, location.hash])

  return (
    <header className="fixed top-0 inset-x-0 z-50 bg-surface-container-lowest/90 backdrop-blur-md shadow-[0_1px_8px_rgba(0,0,0,0.04)]">
      <div className="h-20 max-w-[1360px] mx-auto px-3 sm:px-gutter flex items-center justify-between gap-3 lg:gap-space-lg">
        <Link to="/" className="flex items-center shrink-0 min-w-0" aria-label="Part Finder home">
          <img
            alt="Part Finder — Search with Precision"
            className="h-14 sm:h-16 w-auto object-contain block"
            src={LOGO_SRC}
          />
        </Link>
        <nav className="hidden xl:flex items-center gap-3 2xl:gap-space-lg" aria-label="Main">
          {NAV.map((item) => {
            const active = isNavItemActive(item, location.pathname, onHome)
            return (
              <Link
                key={item.label}
                to={item.href}
                aria-current={active ? 'page' : undefined}
                className={navLinkClass(active)}
              >
                {item.label}
              </Link>
            )
          })}
        </nav>
        <div className="flex items-center gap-2 md:gap-space-md shrink min-w-0">
          <Link
            to="/products"
            className={`xl:hidden ${navLinkClass(onProducts)} px-1 hidden sm:inline`}
            aria-current={onProducts ? 'page' : undefined}
          >
            Products
          </Link>
          <Link
            to="/industries"
            className={`xl:hidden ${navLinkClass(onIndustries)} px-1 hidden sm:inline`}
            aria-current={onIndustries ? 'page' : undefined}
          >
            Industries
          </Link>
          <button
            type="button"
            className="xl:hidden w-10 h-10 rounded-lg bg-surface-container text-on-surface border-0 flex items-center justify-center hover:bg-surface-container-high"
            aria-expanded={mobileNavOpen}
            aria-controls="site-mobile-nav"
            aria-label={mobileNavOpen ? 'Close menu' : 'Open menu'}
            onClick={() => setMobileNavOpen((open) => !open)}
          >
            <Icon name={mobileNavOpen ? 'close' : 'menu'} className="text-[22px]" />
          </button>
          {account ? (
            <div className="flex items-center gap-2 min-w-0">
              <span className="font-label-md text-label-md text-on-surface truncate max-w-[140px] sm:max-w-[220px]" title={account.email}>
                {account.email}
              </span>
              <button
                type="button"
                className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface bg-transparent border-0 px-1 whitespace-nowrap"
                onClick={() => {
                  clearAccountSession()
                  setAccount(null)
                }}
              >
                Sign out
              </button>
            </div>
          ) : (
            <Link
              to="/sign-in"
              className={
                onSignIn
                  ? 'font-label-md text-label-md text-primary font-bold transition-colors px-1 sm:px-space-xs whitespace-nowrap'
                  : 'font-label-md text-label-md text-on-surface-variant hover:text-on-surface transition-colors px-1 sm:px-space-xs whitespace-nowrap'
              }
            >
              Sign In
            </Link>
          )}
          <Link
            to="/get-licence"
            className={
              onLicence
                ? 'bg-primary text-on-primary font-label-md text-label-md px-2.5 py-1.5 sm:px-space-md sm:py-space-sm rounded-lg shadow-[0_4px_12px_rgba(37,99,235,0.25)] whitespace-nowrap'
                : 'bg-primary-container text-on-primary font-label-md text-label-md px-2.5 py-1.5 sm:px-space-md sm:py-space-sm rounded-lg hover:bg-primary transition-colors shadow-[0_4px_12px_rgba(37,99,235,0.25)] whitespace-nowrap'
            }
          >
            <span className="sm:hidden">Licence</span>
            <span className="hidden sm:inline">Get Licence</span>
          </Link>
          <DownloadLink className="hidden lg:inline-flex" />
        </div>
      </div>
      {mobileNavOpen ? (
        <nav
          id="site-mobile-nav"
          className="xl:hidden border-t border-outline-variant/20 bg-surface-container-lowest/95 backdrop-blur-md max-h-[min(70vh,420px)] overflow-y-auto"
          aria-label="Main mobile"
        >
          <div className="max-w-[1360px] mx-auto px-gutter py-space-md flex flex-col gap-1">
            {NAV.map((item) => {
              const active = isNavItemActive(item, location.pathname, onHome)
              return (
                <Link
                  key={item.label}
                  to={item.href}
                  className={`${navLinkClass(active)} py-2.5 px-2 rounded-lg hover:bg-surface-container-low`}
                  aria-current={active ? 'page' : undefined}
                >
                  {item.label}
                </Link>
              )
            })}
          </div>
        </nav>
      ) : null}
    </header>
  )
}

function FooterColumn({
  title,
  links,
}: {
  title: string
  links: { label: string; href: string; external?: boolean; icon?: string }[]
}) {
  return (
    <div className="flex flex-col gap-space-sm">
      <span className="font-label-md text-label-md text-surface-variant uppercase tracking-wider font-semibold mb-space-xs">
        {title}
      </span>
      {links.map((link, index) =>
        link.external ? (
          <a
            key={`${link.label}-${index}`}
            className="font-body-sm text-body-sm text-secondary-fixed hover:text-surface-bright transition-colors flex items-center gap-1"
            href={link.href}
            target="_blank"
            rel="noopener noreferrer"
          >
            {link.icon ? <Icon name={link.icon} className="text-[16px]" /> : null}
            {link.label}
          </a>
        ) : (
          <Link
            key={`${link.label}-${index}`}
            className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors"
            to={link.href}
          >
            {link.label}
          </Link>
        ),
      )}
    </div>
  )
}

export function SiteFooter() {
  return (
    <footer className="w-full bg-inverse-surface text-inverse-on-surface pt-space-xl pb-space-lg">
      <div className="max-w-[1360px] mx-auto px-gutter">
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-gutter-lg pb-space-xl">
          <FooterColumn
            title="Platform"
            links={[
              { label: 'All products', href: '/products' },
              { label: 'AI Smart Search', href: '/products#ai-smart-search' },
              { label: 'Inventory Intelligence', href: '/products#inventory-intelligence' },
              { label: 'Supplier Network', href: '/products#supplier-network' },
              { label: 'Technical Specs', href: '/products#technical-specs' },
              { label: 'API & Integrations', href: '/products#api-integrations' },
            ]}
          />
          <FooterColumn
            title="Solutions"
            links={[
              { label: 'Fleet & Vessel', href: '/#solutions' },
              { label: 'Maintenance & MRO', href: '/#solutions' },
              { label: 'Procurement Optimisation', href: '/#solutions' },
              { label: 'OEM & Aftermarket', href: '/#solutions' },
            ]}
          />
          <FooterColumn
            title="Industries"
            links={[
              { label: 'All industries', href: '/industries' },
              { label: 'Marine', href: '/industries#marine' },
              { label: 'Automotive', href: '/industries#automotive' },
              { label: 'Aviation', href: '/industries#aviation' },
              { label: 'Industrial & Plants', href: '/industries#industrial-plants' },
              { label: 'Electronics', href: '/industries#electronics' },
            ]}
          />
          <FooterColumn
            title="Resources"
            links={[
              { label: 'All resources', href: '/resources' },
              { label: 'Documentation & API', href: '/resources#api' },
              { label: 'IMPA/ISSA Guides', href: '/industries#marine' },
              { label: 'Case Studies', href: '/#impact' },
              { label: 'Release notes', href: '/resources#release-notes' },
              { label: 'System Status', href: '/resources#system-status' },
              { label: 'Download Windows App', href: STORE_URL, external: true, icon: 'download' },
            ]}
          />
          <FooterColumn
            title="Company"
            links={[
              { label: 'About Us', href: '/#impact' },
              { label: 'Leadership', href: '/#impact' },
              { label: 'Careers', href: '/#architect' },
              { label: 'Press', href: '/#impact' },
              { label: 'Contact Sales', href: '/#architect' },
            ]}
          />
        </div>
        <div className="pt-space-lg flex flex-col md:flex-row items-center justify-between gap-space-md">
          <div className="flex flex-wrap items-center gap-space-md">
            <span className="font-body-sm text-body-sm text-outline-variant">
              © 2025 PartFinder Inc. All rights reserved.
            </span>
            <span className="font-caption text-caption text-secondary-fixed bg-on-secondary-fixed-variant px-space-xs py-0.5 rounded">
              ISO 27001 Certified
            </span>
            <span className="font-caption text-caption text-secondary-fixed bg-on-secondary-fixed-variant px-space-xs py-0.5 rounded">
              SOC2 Type II
            </span>
          </div>
          <div className="flex flex-wrap items-center gap-space-md">
            <a className="font-caption text-caption text-outline-variant hover:text-surface-bright transition-colors" href="#top">
              Terms of Service
            </a>
            <a className="font-caption text-caption text-outline-variant hover:text-surface-bright transition-colors" href="#top">
              Privacy Policy
            </a>
            <a className="font-caption text-caption text-outline-variant hover:text-surface-bright transition-colors" href="#top">
              Cookie Settings
            </a>
          </div>
        </div>
      </div>
    </footer>
  )
}

export function MarketingFrame({ children }: { children: ReactNode }) {
  useEffect(() => {
    document.documentElement.classList.add('pf-site')
    return () => {
      document.documentElement.classList.remove('pf-site')
    }
  }, [])

  return (
    <div id="top" className="pf-marketing min-h-screen bg-background font-body-md text-on-surface">
      <SiteHeader />
      {children}
      <SiteFooter />
    </div>
  )
}

/** Admin login/register — no marketing nav (URLs are typed directly). */
export function AdminFrame({ children }: { children: ReactNode }) {
  useEffect(() => {
    document.documentElement.classList.add('pf-site')
    return () => {
      document.documentElement.classList.remove('pf-site')
    }
  }, [])

  return (
    <div className="pf-marketing min-h-screen bg-background font-body-md text-on-surface">
      {children}
    </div>
  )
}
