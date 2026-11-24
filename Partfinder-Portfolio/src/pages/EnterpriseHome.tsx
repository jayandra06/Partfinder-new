import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { PartFinderLogo } from '../components/PartFinderLogo'
import '../enterprise.css'

const ADMIN_TOKEN_KEY = 'pf_admin_token'

const apiBase =
  import.meta.env.VITE_API_URL?.replace(/\/$/, '') ??
  (import.meta.env.DEV ? '' : 'https://partfinder-backend.vercel.app')

function readStoredToken() {
  if (typeof window === 'undefined') {
    return null
  }
  const token = window.localStorage.getItem(ADMIN_TOKEN_KEY)
  if (!token || !isUsableAccessToken(token)) {
    if (token) {
      window.localStorage.removeItem(ADMIN_TOKEN_KEY)
    }
    return null
  }
  return token
}

function isUsableAccessToken(token: string) {
  const parts = token.split('.')
  if (parts.length !== 3) {
    return false
  }
  try {
    const payload = JSON.parse(
      atob(parts[1].replace(/-/g, '+').replace(/_/g, '/')),
    ) as { exp?: number; sub?: string }
    return (
      typeof payload.exp === 'number' &&
      typeof payload.sub === 'string' &&
      payload.sub.length > 0 &&
      payload.exp * 1000 > Date.now()
    )
  } catch {
    return false
  }
}

export function EnterpriseHome() {
  const [isLoggedIn, setIsLoggedIn] = useState(false)

  useEffect(() => {
    document.body.classList.add('pf-home')
    document.title = 'PartFinder'
    let cancelled = false

    const syncAuth = async () => {
      const token = readStoredToken()
      if (!token) {
        if (!cancelled) {
          setIsLoggedIn(false)
        }
        return
      }
      try {
        const response = await fetch(`${apiBase}/api/dashboard/stats`, {
          headers: { Authorization: `Bearer ${token}` },
        })
        if (response.status === 401 || response.status === 403) {
          window.localStorage.removeItem(ADMIN_TOKEN_KEY)
          if (!cancelled) {
            setIsLoggedIn(false)
          }
          return
        }
        if (!cancelled) {
          setIsLoggedIn(true)
        }
      } catch {
        if (!cancelled) {
          setIsLoggedIn(false)
        }
      }
    }

    void syncAuth()
    const onFocus = () => {
      void syncAuth()
    }
    window.addEventListener('focus', onFocus)
    window.addEventListener('storage', onFocus)
    return () => {
      cancelled = true
      document.body.classList.remove('pf-home')
      document.title = 'partfinder-portfolio'
      window.removeEventListener('focus', onFocus)
      window.removeEventListener('storage', onFocus)
    }
  }, [])

  return (
    <div className="pf-site bg-background font-body-md text-on-surface">
      <header className="fixed top-0 left-0 w-full z-50 bg-surface-container-lowest/90 backdrop-blur-md shadow-[0_1px_8px_rgba(0,0,0,0.04)]"><div className="h-20 max-w-[1360px] mx-auto px-gutter flex items-center justify-between gap-space-lg"><Link to="/" onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })} className="flex items-center gap-space-md cursor-pointer hover:opacity-90 transition-opacity"><PartFinderLogo variant="light" className="pf-nav-logo" /><div className="flex flex-col"><span className="font-headline-sm text-headline-sm text-on-surface tracking-tight leading-none">PartFinder</span></div></Link><nav className="hidden xl:flex items-center gap-space-lg" data-active-classes="text-primary font-label-md font-bold"><a aria-current="page" className="transition-colors text-primary font-label-md font-bold" data-path="products" href="#">Products</a><a className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface transition-colors" data-path="solutions" href="#">Solutions</a><a className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface transition-colors" data-path="industries" href="#">Industries</a><a className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface transition-colors" data-path="features" href="#">Features</a><a className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface transition-colors" data-path="pricing" href="#">Pricing</a><a className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface transition-colors" data-path="resources" href="#">Resources</a><a className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface transition-colors" data-path="about" href="#">About</a><a className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface transition-colors" data-path="contact" href="#">Contact</a></nav><div className="flex items-center gap-space-md"><a className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface transition-colors px-space-xs" data-path="login" href="/admin">Sign In</a><a href="https://apps.microsoft.com/detail/9N6XVKPX189W?hl=en-us&amp;gl=IN&amp;ocid=pdpshare" target="_blank" rel="noopener noreferrer" className="hidden sm:inline-flex items-center gap-1.5 border border-surface-container-high/80 bg-surface-container-low/70 hover:bg-surface-container text-on-surface font-label-md text-label-md px-space-md py-space-sm rounded-lg transition-colors shadow-sm"><span className="material-symbols-outlined text-primary text-[18px]">download</span><span className="">Download App</span></a><a className="bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-space-sm rounded-lg hover:bg-primary transition-colors shadow-[0_4px_12px_rgba(37,99,235,0.25)]" data-path="request-demo" href="#">Request Demo</a>{isLoggedIn ? <a href="/admin" aria-label="Account" className="w-8 h-8 rounded-full bg-primary flex items-center justify-center"><span className="material-symbols-outlined text-on-primary text-[18px]">person</span></a> : null}</div></div></header><main className="w-full pt-20 bg-background"><div className="flex flex-col w-full"> <section className="relative w-full pt-space-lg pb-space-xl overflow-hidden bg-background"> <div className="absolute inset-0 bg-gradient-to-br from-primary-fixed/20 via-transparent to-secondary-fixed/15 pointer-events-none"></div>
<div className="max-w-[1360px] mx-auto px-gutter relative z-10">
<div> <div className="flex flex-col gap-space-md max-w-3xl"> <div className="inline-flex items-center gap-space-xs self-start px-space-sm py-1 bg-surface-container rounded-full shadow-sm">
<span className="material-symbols-outlined text-secondary text-[16px]" style={{ fontVariationSettings: "'FILL' 1" }}>verified</span>
<span className="font-caption text-caption text-on-surface font-semibold tracking-wide"> Next-Generation Industrial Intelligence • ISO 27001 Certified </span>
</div> <h1 className="font-display-hero text-display-hero text-on-surface tracking-tight"> Find the right part. <br />
<span className="text-primary-container">Faster.</span>
</h1> <p className="font-body-lg text-body-lg text-on-surface-variant max-w-xl"> An intelligent platform for discovering, managing, and procuring marine, automotive, aviation, and industrial spare parts across global supply networks. </p> <div className="flex flex-wrap items-center gap-space-md pt-space-xs">
<a className="inline-flex items-center justify-center gap-space-xs bg-primary-container text-on-primary font-label-md text-label-md px-space-lg py-3 rounded-lg shadow-md hover:bg-primary transition-all" href="#">
<span className="">Explore Platform</span>
<span className="material-symbols-outlined text-[18px]">arrow_forward</span>
</a>
<a className="inline-flex items-center justify-center gap-space-xs bg-surface-container-lowest text-on-surface font-label-md text-label-md px-space-md py-3 rounded-lg shadow-sm hover:bg-surface-container-low transition-all" href="#">
<span className="material-symbols-outlined text-primary text-[20px]" style={{ fontVariationSettings: "'FILL' 1" }}>play_circle</span>
<span className="">Request a Demo</span>
</a>
<a href="https://apps.microsoft.com/detail/9N6XVKPX189W?hl=en-us&amp;gl=IN&amp;ocid=pdpshare" target="_blank" rel="noopener noreferrer" className="inline-flex items-center justify-center gap-space-xs border border-surface-container-high bg-surface-container-lowest text-on-surface font-label-md text-label-md px-space-md py-3 rounded-lg shadow-sm hover:bg-surface-container-low transition-all"><span className="material-symbols-outlined text-primary text-[20px]">desktop_windows</span><span className="">Download for Windows</span></a></div>
</div> 
</div>
</div>
</section> <section className="w-full bg-surface-container-lowest py-space-xl">
<div className="max-w-[1360px] mx-auto px-gutter">
<div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-gutter-lg"> <div className="bg-surface-container-low p-space-lg rounded-xl flex flex-col justify-between shadow-sm hover:shadow-md transition-shadow">
<div className="flex items-center justify-between mb-space-sm">
<span className="material-symbols-outlined text-primary text-[32px]">inventory_2</span>
<span className="bg-primary-fixed text-on-primary-fixed font-caption text-caption px-2 py-0.5 rounded-full font-bold">ENGINEERING VERIFIED</span>
</div>
<div>
<div className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight">50K+</div>
<div className="font-label-md text-label-md text-primary font-bold mt-1">Parts Catalogued</div>
<p className="font-body-sm text-body-sm text-on-surface-variant mt-2"> Verified dimensional technical specifications, 3D CAD models, and manufacturer drawings. </p>
</div>
</div> <div className="bg-surface-container-low p-space-lg rounded-xl flex flex-col justify-between shadow-sm hover:shadow-md transition-shadow">
<div className="flex items-center justify-between mb-space-sm">
<span className="material-symbols-outlined text-secondary text-[32px]">local_shipping</span>
<span className="bg-secondary-fixed text-on-secondary-fixed-variant font-caption text-caption px-2 py-0.5 rounded-full font-bold">GLOBAL REACH</span>
</div>
<div>
<div className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight">1K+</div>
<div className="font-label-md text-label-md text-secondary font-bold mt-1">Global Suppliers</div>
<p className="font-body-sm text-body-sm text-on-surface-variant mt-2"> Audited Tier-1 OEM vendors, authorized distributors, and bonded warehouse operators. </p>
</div>
</div> <div className="bg-surface-container-low p-space-lg rounded-xl flex flex-col justify-between shadow-sm hover:shadow-md transition-shadow">
<div className="flex items-center justify-between mb-space-sm">
<span className="material-symbols-outlined text-primary text-[32px]">model_training</span>
<span className="bg-surface-variant text-on-primary-fixed-variant font-caption text-caption px-2 py-0.5 rounded-full font-bold">MULTIMODAL AI</span>
</div>
<div>
<div className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight">99%</div>
<div className="font-label-md text-label-md text-primary font-bold mt-1">Search Accuracy</div>
<p className="font-body-sm text-body-sm text-on-surface-variant mt-2"> Visual matching, damaged label OCR, and cross-standard IMPA/ISSA/NATO mapping. </p>
</div>
</div> <div className="bg-surface-container-low p-space-lg rounded-xl flex flex-col justify-between shadow-sm hover:shadow-md transition-shadow">
<div className="flex items-center justify-between mb-space-sm">
<span className="material-symbols-outlined text-tertiary text-[32px]">precision_manufacturing</span>
<span className="bg-tertiary-fixed text-on-tertiary-fixed font-caption text-caption px-2 py-0.5 rounded-full font-bold">CRITICAL DOMAINS</span>
</div>
<div>
<div className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight">5+</div>
<div className="font-label-md text-label-md text-tertiary font-bold mt-1">Heavy Industries</div>
<p className="font-body-sm text-body-sm text-on-surface-variant mt-2"> Dedicated compliance stacks across Marine, Aviation, Automotive, Plant &amp; Electronics. </p>
</div>
</div>
</div>
</div>
</section> <section className="w-full bg-surface-container py-space-xl">
<div className="max-w-[1360px] mx-auto px-gutter">
<div className="flex flex-col md:flex-row md:items-end justify-between mb-space-lg gap-space-sm">
<div>
<span className="font-caption text-caption text-secondary uppercase tracking-widest font-bold">Sectors &amp; Standards</span>
<h2 className="font-headline-lg text-headline-lg text-on-surface tracking-tight mt-1">Engineered for mission-critical operations.</h2>
</div>
<p className="font-body-sm text-body-sm text-on-surface-variant max-w-md"> Parametric nomenclature mapped directly to industry classification codes, class societies, and export regimens. </p>
</div> <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-space-md"> <div className="bg-surface-container-lowest p-space-md rounded-xl shadow-sm hover:shadow-md transition-all flex flex-col justify-between group cursor-pointer">
<div className="flex items-center justify-between mb-space-sm">
<div className="w-10 h-10 rounded-lg bg-surface-container-high flex items-center justify-center text-primary group-hover:bg-primary-container group-hover:text-on-primary transition-colors">
<span className="material-symbols-outlined text-[24px]">directions_boat</span>
</div>
<span className="font-caption text-caption text-tertiary font-mono">IMPA / ISSA</span>
</div>
<div>
<h3 className="font-headline-sm text-headline-sm text-on-surface mb-1">Marine</h3>
<p className="font-body-sm text-body-sm text-on-surface-variant"> IMPA/ISSA mapped catalogs, deck &amp; engine stores, vessel MRO, class society certifications. </p>
</div>
<div className="mt-space-md pt-space-xs text-primary font-label-sm text-label-sm flex items-center gap-1 font-bold">
<span className="">Explore Marine</span>
<span className="material-symbols-outlined text-[16px]">arrow_forward</span>
</div>
</div> <div className="bg-surface-container-lowest p-space-md rounded-xl shadow-sm hover:shadow-md transition-all flex flex-col justify-between group cursor-pointer">
<div className="flex items-center justify-between mb-space-sm">
<div className="w-10 h-10 rounded-lg bg-surface-container-high flex items-center justify-center text-primary group-hover:bg-primary-container group-hover:text-on-primary transition-colors">
<span className="material-symbols-outlined text-[24px]">directions_car</span>
</div>
<span className="font-caption text-caption text-tertiary font-mono">OEM / IAM</span>
</div>
<div>
<h3 className="font-headline-sm text-headline-sm text-on-surface mb-1">Automotive</h3>
<p className="font-body-sm text-body-sm text-on-surface-variant"> OEM and aftermarket cross-referencing, commercial fleet drivetrain, and heavy truck spares. </p>
</div>
<div className="mt-space-md pt-space-xs text-primary font-label-sm text-label-sm flex items-center gap-1 font-bold">
<span className="">Explore Auto</span>
<span className="material-symbols-outlined text-[16px]">arrow_forward</span>
</div>
</div> <div className="bg-surface-container-lowest p-space-md rounded-xl shadow-sm hover:shadow-md transition-all flex flex-col justify-between group cursor-pointer">
<div className="flex items-center justify-between mb-space-sm">
<div className="w-10 h-10 rounded-lg bg-surface-container-high flex items-center justify-center text-primary group-hover:bg-primary-container group-hover:text-on-primary transition-colors">
<span className="material-symbols-outlined text-[24px]">flight</span>
</div>
<span className="font-caption text-caption text-secondary font-mono font-bold">FAA / EASA</span>
</div>
<div>
<h3 className="font-headline-sm text-headline-sm text-on-surface mb-1">Aviation</h3>
<p className="font-body-sm text-body-sm text-on-surface-variant"> FAA 8130 / EASA Form 1 traceability, avionics, landing gear components, serialized tracking. </p>
</div>
<div className="mt-space-md pt-space-xs text-primary font-label-sm text-label-sm flex items-center gap-1 font-bold">
<span className="">Explore Aviation</span>
<span className="material-symbols-outlined text-[16px]">arrow_forward</span>
</div>
</div> <div className="bg-surface-container-lowest p-space-md rounded-xl shadow-sm hover:shadow-md transition-all flex flex-col justify-between group cursor-pointer">
<div className="flex items-center justify-between mb-space-sm">
<div className="w-10 h-10 rounded-lg bg-surface-container-high flex items-center justify-center text-primary group-hover:bg-primary-container group-hover:text-on-primary transition-colors">
<span className="material-symbols-outlined text-[24px]">factory</span>
</div>
<span className="font-caption text-caption text-tertiary font-mono">PLANT / MRO</span>
</div>
<div>
<h3 className="font-headline-sm text-headline-sm text-on-surface mb-1">Industrial</h3>
<p className="font-body-sm text-body-sm text-on-surface-variant"> Heavy plant equipment, hydraulic valves, compressors, turbine kits, and automated assembly spares. </p>
</div>
<div className="mt-space-md pt-space-xs text-primary font-label-sm text-label-sm flex items-center gap-1 font-bold">
<span className="">Explore Industrial</span>
<span className="material-symbols-outlined text-[16px]">arrow_forward</span>
</div>
</div> <div className="bg-surface-container-lowest p-space-md rounded-xl shadow-sm hover:shadow-md transition-all flex flex-col justify-between group cursor-pointer">
<div className="flex items-center justify-between mb-space-sm">
<div className="w-10 h-10 rounded-lg bg-surface-container-high flex items-center justify-center text-primary group-hover:bg-primary-container group-hover:text-on-primary transition-colors">
<span className="material-symbols-outlined text-[24px]">memory</span>
</div>
<span className="font-caption text-caption text-tertiary font-mono">MIL-SPEC</span>
</div>
<div>
<h3 className="font-headline-sm text-headline-sm text-on-surface mb-1">Electronics</h3>
<p className="font-body-sm text-body-sm text-on-surface-variant"> Micro-components, hard-to-find semiconductors, relays, ruggedized PLCs, and circuit protection. </p>
</div>
<div className="mt-space-md pt-space-xs text-primary font-label-sm text-label-sm flex items-center gap-1 font-bold">
<span className="">Explore Electronics</span>
<span className="material-symbols-outlined text-[16px]">arrow_forward</span>
</div>
</div>
</div>
</div>
</section> <section className="w-full bg-background py-space-xl">
<div className="max-w-[1360px] mx-auto px-gutter"> <div className="text-center max-w-3xl mx-auto mb-space-xl">
<span className="font-caption text-caption text-primary uppercase tracking-widest font-bold">Core Infrastructure</span>
<h2 className="font-headline-xl text-headline-xl text-on-surface tracking-tight mt-1"> Precision capabilities built for zero-downtime operations. </h2>
<p className="font-body-lg text-body-lg text-on-surface-variant mt-space-sm"> A unified enterprise operating suite bridging complex technical taxonomies with automated physical supply pipelines. </p>
</div> <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-gutter-lg"> <div className="bg-surface-container-lowest p-space-lg rounded-xl shadow-sm hover:shadow-lg transition-all flex flex-col justify-between">
<div>
<div className="w-12 h-12 rounded-lg bg-surface-container-high text-primary flex items-center justify-center mb-space-md">
<span className="material-symbols-outlined text-[28px]">search_insights</span>
</div>
<h3 className="font-headline-sm text-headline-sm text-on-surface mb-space-xs">AI Smart Search</h3>
<p className="font-body-md text-body-md text-on-surface-variant mb-space-md"> Multimodal visual part recognition, barcode scan, and technical drawing OCR for instantaneous identification of weathered tags. </p>
</div>
<div className="bg-surface-container-low p-space-sm rounded-lg flex items-center justify-between">
<span className="font-caption text-caption text-tertiary font-mono">Input: Image, PDF, Text</span>
<span className="font-caption text-caption text-primary font-bold">OCR Accuracy: 99.8%</span>
</div>
</div> <div className="bg-surface-container-lowest p-space-lg rounded-xl shadow-sm hover:shadow-lg transition-all flex flex-col justify-between">
<div>
<div className="w-12 h-12 rounded-lg bg-surface-container-high text-secondary flex items-center justify-center mb-space-md">
<span className="material-symbols-outlined text-[28px]">warehouse</span>
</div>
<h3 className="font-headline-sm text-headline-sm text-on-surface mb-space-xs">Inventory Intelligence</h3>
<p className="font-body-md text-body-md text-on-surface-variant mb-space-md"> Real-time stock synchronization across global bonded warehouses with predictive stockout warnings based on fleet operating hours. </p>
</div>
<div className="bg-surface-container-low p-space-sm rounded-lg flex items-center justify-between">
<span className="font-caption text-caption text-tertiary font-mono">Live ERP Sync</span>
<span className="font-caption text-caption text-secondary font-bold">Zero Phantom Stock</span>
</div>
</div> <div className="bg-surface-container-lowest p-space-lg rounded-xl shadow-sm hover:shadow-lg transition-all flex flex-col justify-between">
<div>
<div className="w-12 h-12 rounded-lg bg-surface-container-high text-primary flex items-center justify-center mb-space-md">
<span className="material-symbols-outlined text-[28px]">hub</span>
</div>
<h3 className="font-headline-sm text-headline-sm text-on-surface mb-space-xs">Supplier Network</h3>
<p className="font-body-md text-body-md text-on-surface-variant mb-space-md"> A pre-vetted global procurement pipeline with automated RFQs, real-time freight rates, and certified vendor SLA scoring. </p>
</div>
<div className="bg-surface-container-low p-space-sm rounded-lg flex items-center justify-between">
<span className="font-caption text-caption text-tertiary font-mono">1,000+ Tier-1 Hubs</span>
<span className="font-caption text-caption text-primary font-bold">Automated RFQ</span>
</div>
</div> <div className="bg-surface-container-lowest p-space-lg rounded-xl shadow-sm hover:shadow-lg transition-all flex flex-col justify-between">
<div>
<div className="w-12 h-12 rounded-lg bg-surface-container-high text-tertiary flex items-center justify-center mb-space-md">
<span className="material-symbols-outlined text-[28px]">receipt_long</span>
</div>
<h3 className="font-headline-sm text-headline-sm text-on-surface mb-space-xs">Automated Procurement</h3>
<p className="font-body-md text-body-md text-on-surface-variant mb-space-md"> One-click Purchase Order generation with bi-directional ERP integration for SAP S/4HANA, Oracle NetSuite, and Microsoft Dynamics. </p>
</div>
<div className="bg-surface-container-low p-space-sm rounded-lg flex items-center justify-between">
<span className="font-caption text-caption text-tertiary font-mono">PunchOut / EDI 850</span>
<span className="font-caption text-caption text-tertiary font-bold">3-Minute Cycle</span>
</div>
</div> <div className="bg-surface-container-lowest p-space-lg rounded-xl shadow-sm hover:shadow-lg transition-all flex flex-col justify-between">
<div>
<div className="w-12 h-12 rounded-lg bg-surface-container-high text-primary flex items-center justify-center mb-space-md">
<span className="material-symbols-outlined text-[28px]">view_in_ar</span>
</div>
<h3 className="font-headline-sm text-headline-sm text-on-surface mb-space-xs">Technical Specs Engine</h3>
<p className="font-body-md text-body-md text-on-surface-variant mb-space-md"> Interactive exploded assembly views, embedded 3D WebGL viewer, and complete OEM service manuals with direct click-to-cart tags. </p>
</div>
<div className="bg-surface-container-low p-space-sm rounded-lg flex items-center justify-between">
<span className="font-caption text-caption text-tertiary font-mono">STEP / IGES Native</span>
<span className="font-caption text-caption text-primary font-bold">Interactive Schematics</span>
</div>
</div> <div className="bg-surface-container-lowest p-space-lg rounded-xl shadow-sm hover:shadow-lg transition-all flex flex-col justify-between">
<div>
<div className="w-12 h-12 rounded-lg bg-surface-container-high text-secondary flex items-center justify-center mb-space-md">
<span className="material-symbols-outlined text-[28px]">trending_up</span>
</div>
<h3 className="font-headline-sm text-headline-sm text-on-surface mb-space-xs">Enterprise Analytics</h3>
<p className="font-body-md text-body-md text-on-surface-variant mb-space-md"> Enterprise spend optimization, global price volatility forecasting, and supplier fulfillment SLA benchmarks. </p>
</div>
<div className="bg-surface-container-low p-space-sm rounded-lg flex items-center justify-between">
<span className="font-caption text-caption text-tertiary font-mono">Predictive Index</span>
<span className="font-caption text-caption text-secondary font-bold">14.2% Avg Savings</span>
</div>
</div>
</div>
</div>
</section> <section className="w-full bg-surface-container-low py-space-xl">
<div className="max-w-[1360px] mx-auto px-gutter"> <div className="flex flex-col lg:flex-row lg:items-end justify-between mb-space-xl gap-space-md">
<div>
<div className="inline-flex items-center gap-1.5 px-space-xs py-0.5 rounded bg-surface-container text-primary font-caption text-caption uppercase font-bold mb-space-xs"> Operational Workflow </div>
<h2 className="font-headline-xl text-headline-xl text-on-surface tracking-tight"> Search to Delivery in 3 Clicks. </h2>
</div>
<div className="flex items-center gap-space-sm">
<div className="flex items-center gap-1 bg-surface-container-lowest px-3 py-1.5 rounded-lg shadow-sm">
<span className="w-2 h-2 rounded-full bg-secondary"></span>
<span className="font-caption text-caption font-bold text-on-surface">Step 1: AI Match</span>
</div>
<span className="text-tertiary">→</span>
<div className="flex items-center gap-1 bg-surface-container-lowest px-3 py-1.5 rounded-lg shadow-sm">
<span className="w-2 h-2 rounded-full bg-primary-container"></span>
<span className="font-caption text-caption font-bold text-on-surface">Step 2: Compare &amp; Cross-Ref</span>
</div>
<span className="text-tertiary">→</span>
<div className="flex items-center gap-1 bg-surface-container-lowest px-3 py-1.5 rounded-lg shadow-sm">
<span className="w-2 h-2 rounded-full bg-primary"></span>
<span className="font-caption text-caption font-bold text-on-surface">Step 3: ERP Dispatch</span>
</div>
</div>
</div> <div className="bg-surface-container-lowest rounded-xl shadow-xl overflow-hidden p-space-lg"> <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-space-md mb-space-md bg-surface-container-low p-space-sm rounded-lg gap-space-sm">
<div className="flex items-center gap-space-sm">
<span className="font-label-md text-label-md text-on-surface font-bold">Active Batch Order:</span>
<span className="font-mono-data text-mono-data bg-surface-container px-2 py-0.5 rounded text-primary font-semibold">BATCH-2025-EU-8841</span>
<span className="bg-secondary-fixed text-on-secondary-fixed-variant font-caption text-caption px-2 py-0.5 rounded-full font-bold">4 Verified Lines</span>
</div>
<div className="flex items-center gap-space-xs">
<button className="bg-surface-container-lowest text-on-surface font-label-sm text-label-sm px-space-sm py-1.5 rounded shadow-sm hover:bg-surface-container flex items-center gap-1">
<span className="material-symbols-outlined text-[16px]">file_download</span>
<span className="">Export CSV</span>
</button>
<button className="bg-primary-container text-on-primary font-label-sm text-label-sm px-space-sm py-1.5 rounded shadow-sm hover:bg-primary flex items-center gap-1 font-bold">
<span className="material-symbols-outlined text-[16px]">send</span>
<span className="">Dispatch to SAP</span>
</button>
</div>
</div> <div className="overflow-x-auto w-full">
<table className="w-full text-left">
<thead><tr className="bg-surface-container text-tertiary font-caption text-caption uppercase tracking-wider"><th className="py-space-sm px-space-md">Item &amp; OEM Part #</th><th className="py-space-sm px-space-md">Classification</th><th className="py-space-sm px-space-md">Direct Cross-Ref</th><th className="py-space-sm px-space-md">Supplier Hub</th><th className="py-space-sm px-space-md">Unit Rate</th><th className="py-space-sm px-space-md">Compliance Status</th><th className="py-space-sm px-space-md text-right">Actions</th></tr></thead>
<tbody className="divide-y divide-surface-container font-mono-data text-body-sm text-on-surface"><tr className="hover:bg-surface-container-low transition-colors"><td className="py-space-sm px-space-md">
<div className="font-body-md font-bold text-on-surface">SKF 6205-2RSH</div>
<span className="font-caption text-caption text-tertiary">Deep Groove Ball Bearing 25x52x15</span>
</td><td className="py-space-sm px-space-md">
<span className="bg-surface-container px-2 py-0.5 rounded font-caption text-caption text-on-surface font-semibold">IMPA 232141</span>
</td><td className="py-space-sm px-space-md">
<span className="text-secondary font-bold">FAG 6205-2RSR</span>
<span className="block font-caption text-caption text-tertiary">100% Tolerance Parity</span>
</td><td className="py-space-sm px-space-md font-body-sm text-body-sm"> Rotterdam Dep. 3 <span className="block font-caption text-caption text-primary">In Stock (1,840)</span>
</td><td className="py-space-sm px-space-md font-bold text-primary">$18.40</td><td className="py-space-sm px-space-md">
<span className="bg-secondary-fixed text-on-secondary-fixed-variant px-2 py-0.5 rounded font-caption text-caption font-bold inline-flex items-center gap-1">
<span className="material-symbols-outlined text-[12px]">check_circle</span> Pass (ISO 9001) </span>
</td><td className="py-space-sm px-space-md text-right">
<button className="bg-surface-container-high text-primary px-2 py-1 rounded font-caption text-caption font-bold hover:bg-primary-container hover:text-on-primary">View CAD</button>
</td></tr><tr className="hover:bg-surface-container-low transition-colors"><td className="py-space-sm px-space-md">
<div className="font-body-md font-bold text-on-surface">Danfoss EV220B Solenoid Valve</div>
<span className="font-caption text-caption text-tertiary">G 1/2" Servo-Operated 24V DC</span>
</td><td className="py-space-sm px-space-md">
<span className="bg-surface-container px-2 py-0.5 rounded font-caption text-caption text-on-surface font-semibold">ISSA 75.521.04</span>
</td><td className="py-space-sm px-space-md">
<span className="text-secondary font-bold">Burkert 5282</span>
<span className="block font-caption text-caption text-tertiary">Flow Coeff. Equal</span>
</td><td className="py-space-sm px-space-md font-body-sm text-body-sm"> Hamburg Logistics Hub <span className="block font-caption text-caption text-primary">In Stock (340)</span>
</td><td className="py-space-sm px-space-md font-bold text-primary">$142.00</td><td className="py-space-sm px-space-md">
<span className="bg-secondary-fixed text-on-secondary-fixed-variant px-2 py-0.5 rounded font-caption text-caption font-bold inline-flex items-center gap-1">
<span className="material-symbols-outlined text-[12px]">check_circle</span> Pass (DNV GL) </span>
</td><td className="py-space-sm px-space-md text-right">
<button className="bg-surface-container-high text-primary px-2 py-1 rounded font-caption text-caption font-bold hover:bg-primary-container hover:text-on-primary">View CAD</button>
</td></tr><tr className="hover:bg-surface-container-low transition-colors"><td className="py-space-sm px-space-md">
<div className="font-body-md font-bold text-on-surface">Bosch Rexroth A10VSO45</div>
<span className="font-caption text-caption text-tertiary">Variable Displacement Axial Piston Pump</span>
</td><td className="py-space-sm px-space-md">
<span className="bg-surface-container px-2 py-0.5 rounded font-caption text-caption text-on-surface font-semibold">NATO Stock 4320-12</span>
</td><td className="py-space-sm px-space-md">
<span className="text-secondary font-bold">Parker PVP48</span>
<span className="block font-caption text-caption text-tertiary">Direct Flange Match</span>
</td><td className="py-space-sm px-space-md font-body-sm text-body-sm"> Houston Bonded FreeZone <span className="block font-caption text-caption text-primary">In Stock (48)</span>
</td><td className="py-space-sm px-space-md font-bold text-primary">$1,890.00</td><td className="py-space-sm px-space-md">
<span className="bg-secondary-fixed text-on-secondary-fixed-variant px-2 py-0.5 rounded font-caption text-caption font-bold inline-flex items-center gap-1">
<span className="material-symbols-outlined text-[12px]">check_circle</span> Pass (ABS Approved) </span>
</td><td className="py-space-sm px-space-md text-right">
<button className="bg-surface-container-high text-primary px-2 py-1 rounded font-caption text-caption font-bold hover:bg-primary-container hover:text-on-primary">View CAD</button>
</td></tr><tr className="hover:bg-surface-container-low transition-colors"><td className="py-space-sm px-space-md">
<div className="font-body-md font-bold text-on-surface">Honeywell Microswitch BZ-2RQ1</div>
<span className="font-caption text-caption text-tertiary">Single Pole Double Throw Roller Lever</span>
</td><td className="py-space-sm px-space-md">
<span className="bg-surface-container px-2 py-0.5 rounded font-caption text-caption text-on-surface font-semibold">MIL-PRF-8805</span>
</td><td className="py-space-sm px-space-md">
<span className="text-secondary font-bold">Omron Z-15GQ22-B</span>
<span className="block font-caption text-caption text-tertiary">15A Contact Match</span>
</td><td className="py-space-sm px-space-md font-body-sm text-body-sm"> Singapore East Depot <span className="block font-caption text-caption text-primary">In Stock (2,400)</span>
</td><td className="py-space-sm px-space-md font-bold text-primary">$14.60</td><td className="py-space-sm px-space-md">
<span className="bg-secondary-fixed text-on-secondary-fixed-variant px-2 py-0.5 rounded font-caption text-caption font-bold inline-flex items-center gap-1">
<span className="material-symbols-outlined text-[12px]">check_circle</span> Pass (FAA Cert) </span>
</td><td className="py-space-sm px-space-md text-right">
<button className="bg-surface-container-high text-primary px-2 py-1 rounded font-caption text-caption font-bold hover:bg-primary-container hover:text-on-primary">View CAD</button>
</td></tr></tbody>
</table>
</div> <div className="mt-space-md pt-space-md bg-surface-container-low p-space-sm rounded-lg flex flex-col sm:flex-row sm:items-center justify-between gap-space-sm">
<div className="flex items-center gap-space-md">
<div>
<span className="font-caption text-caption text-tertiary uppercase">Total Lot Value</span>
<div className="font-headline-sm text-headline-sm text-on-surface font-bold">$2,065.00</div>
</div>
<div className="h-8 w-px bg-surface-container-highest"></div>
<div>
<span className="font-caption text-caption text-tertiary uppercase">Avg Lead Time</span>
<div className="font-headline-sm text-headline-sm text-secondary font-bold">28.4 Hours</div>
</div>
<div className="h-8 w-px bg-surface-container-highest hidden sm:block"></div>
<div className="hidden sm:block">
<span className="font-caption text-caption text-tertiary uppercase">Logistics Multi-Hop</span>
<div className="font-headline-sm text-headline-sm text-on-surface font-bold">Consolidated (1 Waybill)</div>
</div>
</div>
<div className="flex items-center gap-space-xs">
<button className="bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg shadow hover:bg-primary transition-all font-bold"> Execute PO Lock &amp; Dispatch </button>
</div>
</div>
</div>
</div>
</section> <section className="w-full bg-surface-container-lowest py-space-xl">
<div className="max-w-[1360px] mx-auto px-gutter text-center">
<span className="font-caption text-caption text-tertiary uppercase tracking-widest font-bold"> Trusted by Procurement Leaders Across Heavy Industry </span> <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-space-md mt-space-lg items-center">
<div className="bg-surface-container-low p-space-md rounded-xl flex items-center justify-center gap-2 group hover:bg-surface-container transition-colors">
<span className="material-symbols-outlined text-primary text-[28px]">directions_boat</span>
<div className="text-left">
<span className="font-headline-sm text-headline-sm text-on-surface font-bold block leading-none">MAERSK</span>
<span className="font-caption text-caption text-tertiary uppercase font-semibold">Marine Systems</span>
</div>
</div>
<div className="bg-surface-container-low p-space-md rounded-xl flex items-center justify-center gap-2 group hover:bg-surface-container transition-colors">
<span className="material-symbols-outlined text-secondary text-[28px]">bolt</span>
<div className="text-left">
<span className="font-headline-sm text-headline-sm text-on-surface font-bold block leading-none">SIEMENS</span>
<span className="font-caption text-caption text-tertiary uppercase font-semibold">Energy Global</span>
</div>
</div>
<div className="bg-surface-container-low p-space-md rounded-xl flex items-center justify-center gap-2 group hover:bg-surface-container transition-colors">
<span className="material-symbols-outlined text-primary text-[28px]">precision_manufacturing</span>
<div className="text-left">
<span className="font-headline-sm text-headline-sm text-on-surface font-bold block leading-none">ABB</span>
<span className="font-caption text-caption text-tertiary uppercase font-semibold">Industrial Automation</span>
</div>
</div>
<div className="bg-surface-container-low p-space-md rounded-xl flex items-center justify-center gap-2 group hover:bg-surface-container transition-colors">
<span className="material-symbols-outlined text-tertiary text-[28px]">construction</span>
<div className="text-left">
<span className="font-headline-sm text-headline-sm text-on-surface font-bold block leading-none">CATERPILLAR</span>
<span className="font-caption text-caption text-tertiary uppercase font-semibold">MRO Logistics</span>
</div>
</div>
<div className="bg-surface-container-low p-space-md rounded-xl flex items-center justify-center gap-2 group hover:bg-surface-container transition-colors">
<span className="material-symbols-outlined text-secondary text-[28px]">4g_mobiledata</span>
<div className="text-left">
<span className="font-headline-sm text-headline-sm text-on-surface font-bold block leading-none">WÄRTSILÄ</span>
<span className="font-caption text-caption text-tertiary uppercase font-semibold">Power &amp; Propulsion</span>
</div>
</div>
</div>
</div>
</section> <section className="w-full bg-surface-container py-space-xl relative overflow-hidden">
<div className="max-w-[1360px] mx-auto px-gutter relative z-10">
<div className="bg-inverse-surface rounded-2xl p-space-xl text-inverse-on-surface shadow-2xl relative overflow-hidden"> <div className="absolute -right-20 -bottom-20 w-96 h-96 rounded-full bg-primary/20 blur-3xl pointer-events-none"></div>
<div className="max-w-3xl flex flex-col gap-space-md relative z-10">
<div className="inline-flex items-center gap-space-xs self-start px-space-sm py-1 bg-surface-container-highest/20 rounded-full">
<span className="w-2 h-2 rounded-full bg-secondary-fixed"></span>
<span className="font-caption text-caption text-secondary-fixed font-bold tracking-widest uppercase"> Immediate ROI Deployment </span>
</div>
<h2 className="font-headline-xl text-headline-xl text-surface-bright tracking-tight"> Ready to streamline your enterprise parts operations? </h2>
<p className="font-body-lg text-body-lg text-outline-variant"> Join 450+ global fleets and industrial facilities minimizing downtime today. Connect your ERP and experience precision intelligence in under 48 hours. </p>
<div className="flex flex-wrap items-center gap-space-md pt-space-sm">
<a className="inline-flex items-center justify-center gap-space-xs bg-primary-container text-on-primary font-label-md text-label-md px-space-lg py-3.5 rounded-lg shadow-lg hover:bg-primary transition-all font-bold" href="#">
<span className="">Schedule Live Platform Walkthrough</span>
<span className="material-symbols-outlined text-[18px]">calendar_month</span>
</a>
<a className="inline-flex items-center justify-center gap-space-xs bg-surface-variant/20 text-surface-bright font-label-md text-label-md px-space-md py-3.5 rounded-lg hover:bg-surface-variant/30 transition-all font-semibold" href="#">
<span className="">Start Free Pilot</span>
<span className="material-symbols-outlined text-[18px]">arrow_forward</span>
</a>
</div> <div className="flex flex-wrap items-center gap-space-md pt-space-xs text-outline-variant font-caption text-caption">
<span className="flex items-center gap-1">
<span className="material-symbols-outlined text-[16px]">lock</span> SOC2 Type II Audited </span>
<span className="">•</span>
<span className="flex items-center gap-1">
<span className="material-symbols-outlined text-[16px]">verified_user</span> End-to-End Encrypted ERP Pipe </span>
<span className="">•</span>
<span className="flex items-center gap-1">
<span className="material-symbols-outlined text-[16px]">support_agent</span> Dedicated Engineering Architect Support </span>
</div>
</div>
</div>
</div>
</section>
</div></main><footer className="w-full bg-inverse-surface text-inverse-on-surface pt-space-xl pb-space-lg"><div className="max-w-[1360px] mx-auto px-gutter"><Link to="/" onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })} className="inline-flex items-center gap-space-sm pb-space-lg cursor-pointer hover:opacity-90 transition-opacity"><PartFinderLogo variant="dark" className="pf-footer-logo" /><span className="font-headline-sm text-headline-sm text-surface-bright">PartFinder</span></Link><div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-gutter-lg pb-space-xl"><div className="flex flex-col gap-space-sm"><span className="font-label-md text-label-md text-surface-variant uppercase tracking-wider font-semibold mb-space-xs">Platform</span><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="smart-search" href="#">AI Smart Search</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="inventory-intelligence" href="#">Inventory Intelligence</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="supplier-network" href="#">Supplier Network</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="technical-specs" href="#">Technical Specs</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="api-integrations" href="#">API &amp; Integrations</a></div><div className="flex flex-col gap-space-sm"><span className="font-label-md text-label-md text-surface-variant uppercase tracking-wider font-semibold mb-space-xs">Solutions</span><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="fleet-vessel" href="#">Fleet &amp; Vessel</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="maintenance-mro" href="#">Maintenance &amp; MRO</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="procurement-optimization" href="#">Procurement Optimization</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="oem-aftermarket" href="#">OEM &amp; Aftermarket</a></div><div className="flex flex-col gap-space-sm"><span className="font-label-md text-label-md text-surface-variant uppercase tracking-wider font-semibold mb-space-xs">Industries</span><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="marine" href="#">Marine</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="automotive" href="#">Automotive</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="aviation" href="#">Aviation</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="industrial-plants" href="#">Industrial &amp; Plants</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="electronics" href="#">Electronics</a></div><div className="flex flex-col gap-space-sm"><span className="font-label-md text-label-md text-surface-variant uppercase tracking-wider font-semibold mb-space-xs">Resources</span><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="documentation" href="#">Documentation</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="impa-issa-guides" href="#">IMPA/ISSA Guides</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="case-studies" href="#">Case Studies</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="whitepapers" href="#">Whitepapers</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="system-status" href="#">System Status</a></div><div className="flex flex-col gap-space-sm"><span className="font-label-md text-label-md text-surface-variant uppercase tracking-wider font-semibold mb-space-xs">Company</span><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="about" href="#">About Us</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="leadership" href="#">Leadership</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="careers" href="#">Careers</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="press" href="#">Press</a><a className="font-body-sm text-body-sm text-outline-variant hover:text-surface-bright transition-colors" data-path="contact" href="#">Contact Sales</a></div></div><div className="pt-space-lg flex flex-col md:flex-row items-center justify-between gap-space-md"><div className="flex flex-wrap items-center gap-space-md"><span className="font-body-sm text-body-sm text-outline-variant">© 2025 PartFinder Inc. All rights reserved.</span><span className="font-caption text-caption text-secondary-fixed bg-on-secondary-fixed-variant px-space-xs py-0.5 rounded">ISO 27001 Certified</span><span className="font-caption text-caption text-secondary-fixed bg-on-secondary-fixed-variant px-space-xs py-0.5 rounded">SOC2 Type II</span></div><div className="flex flex-wrap items-center gap-space-md"><a className="font-caption text-caption text-outline-variant hover:text-surface-bright transition-colors" data-path="terms-of-service" href="#">Terms of Service</a><a className="font-caption text-caption text-outline-variant hover:text-surface-bright transition-colors" data-path="privacy-policy" href="#">Privacy Policy</a><a className="font-caption text-caption text-outline-variant hover:text-surface-bright transition-colors" data-path="cookie-settings" href="#">Cookie Settings</a></div></div></div></footer>
    </div>
  )
}
