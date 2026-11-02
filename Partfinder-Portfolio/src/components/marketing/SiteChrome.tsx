import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { clearAccountSession, readAccountSession } from './accountSession'
import type { AccountSession } from './accountSession'

export const LOGO_SRC = '/partfinder-logo.png'

export const STORE_URL =
  'https://apps.microsoft.com/detail/9N6XVKPX189W?hl=en-us&gl=IN&ocid=pdpshare'

const NAV = [
  { label: 'Products', href: '/#solutions' },
  { label: 'Solutions', href: '/#solutions', current: true },
  { label: 'Industries', href: '/#solutions' },
  { label: 'Features', href: '/#impact' },
  { label: 'Pricing', href: '/#impact' },
  { label: 'Resources', href: '/#architect' },
  { label: 'About', href: '/#impact' },
  { label: 'Contact', href: '/#architect' },
] as const

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
  const onHome = location.pathname === '/'
  const onLicence = location.pathname === '/get-licence'
  const onSignIn = location.pathname === '/sign-in'

  useEffect(() => {
    setAccount(readAccountSession())
  }, [location.pathname])

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
        <nav className="hidden xl:flex items-center gap-3 2xl:gap-space-lg">
          {NAV.map((item) => {
            const active = onHome && 'current' in item && item.current
            return (
              <Link
                key={item.label}
                to={item.href}
                aria-current={active ? 'page' : undefined}
                className={
                  active
                    ? 'transition-colors text-primary font-label-md font-bold whitespace-nowrap'
                    : 'font-label-md text-label-md text-on-surface-variant hover:text-on-surface transition-colors whitespace-nowrap'
                }
              >
                {item.label}
              </Link>
            )
          })}
        </nav>
        <div className="flex items-center gap-2 md:gap-space-md shrink min-w-0">
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
              { label: 'AI Smart Search', href: '/#solutions' },
              { label: 'Inventory Intelligence', href: '/#solutions' },
              { label: 'Supplier Network', href: '/#solutions' },
              { label: 'Technical Specs', href: '/#impact' },
              { label: 'API & Integrations', href: '/#architect' },
            ]}
          />
          <FooterColumn
            title="Solutions"
            links={[
              { label: 'Fleet & Vessel', href: '/#solutions' },
              { label: 'Maintenance & MRO', href: '/#solutions' },
              { label: 'Procurement Optimization', href: '/#solutions' },
              { label: 'OEM & Aftermarket', href: '/#solutions' },
            ]}
          />
          <FooterColumn
            title="Industries"
            links={[
              { label: 'Marine', href: '/#solutions' },
              { label: 'Automotive', href: '/#solutions' },
              { label: 'Aviation', href: '/#solutions' },
              { label: 'Industrial & Plants', href: '/#solutions' },
              { label: 'Electronics', href: '/#solutions' },
            ]}
          />
          <FooterColumn
            title="Resources"
            links={[
              { label: 'Documentation', href: '/#architect' },
              { label: 'IMPA/ISSA Guides', href: '/#solutions' },
              { label: 'Case Studies', href: '/#impact' },
              { label: 'Whitepapers', href: '/#impact' },
              { label: 'System Status', href: '/#impact' },
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
