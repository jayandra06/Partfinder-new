import { useState } from 'react'
import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Icon, MarketingFrame } from './SiteChrome'

const SHIP_IMAGE =
  'https://lh3.googleusercontent.com/aida-public/AB6AXuATHtjSfHcu7bYQl5TI2MJpIzWZjy2IUxoPoun3AtSP_wHdaCwHECjjkxkDTtCjge1WsiAiPcAvBzTUYQbP0OSjdCBn61HokCQffIrC_nTaXYgh5Gfx4I5nH3KAW5cxaXE45KagTr4NsPO5fOeLgYfd4H38xfaIU8XdbmIxCbkJwBNxhq0_PcRbV2YgKw0ibr9Yo3bvQTX568oGAMFN9ayXzQRMjYr7cjdxbhiA4uMciiRnUnIFTb0V'

const TECH_IMAGE =
  'https://lh3.googleusercontent.com/aida-public/AB6AXuAmxZH02sI8VglV2KUmD8fbYknOP-g7Atm9stannbiEAGYUwNyX_KxhIqYAC_h8MsEnrT5JNwjySpU0XhCKwKJ9vYcDQEwJ-ZpKo5onBPd4sJ83G9cDgGr5Wh-HeYZoz9qy_G_eexWu3KhAuehK3RA_y-YNZwbvbIUC6V4FoVBYMZwo1mBQZNfxJ1U-S-QmJaa0BB6_0IQcnoVTKBpZMlOKOZ9KxzYuiaP4_BJLj3kDzV-gjcAF1dEE'

const CASE_IMAGE =
  'https://lh3.googleusercontent.com/aida-public/AB6AXuCu4EXZNFi5Kf7g-cazQrwflFrbdezIQxUd26L5uv_WA8M5HHYQwmYkpPwZpiGZ5uqJsQ4SuY3rftBTdAKeSGJK-eJ4i6Vl0NuUmV0Eb3ilO0-VCCV_RO_pozuovc8QN_b-RR0JSr_zsUTPRsQdJZdbpj5mZ14O28gC9XHfkE82eDlC-5_SYxxOGzRFOPNb4MQhKS6H8du5uC-PZVIST5mX28L4TSCQAXKBTCm8oxfR828VUXgQFFEZ'

function Pill({ children }: { children: ReactNode }) {
  return (
    <span className="font-mono-data text-caption bg-surface-container px-2.5 py-1 rounded-full text-on-surface font-semibold">
      {children}
    </span>
  )
}

function ExploreLink() {
  return (
    <a
      className="bg-primary text-on-primary font-label-md text-label-md px-space-md py-space-sm rounded-lg hover:bg-primary-container transition-all flex items-center gap-1 shadow-sm font-semibold"
      href="#architect"
    >
      Explore Solution
      <Icon name="arrow_forward" className="text-[18px]" />
    </a>
  )
}

function KpiCard({
  label,
  value,
  meta,
  metaClass,
  barClass,
  barWidth,
  metaIcon,
}: {
  label: string
  value: string
  meta: string
  metaClass: string
  barClass: string
  barWidth: string
  metaIcon?: string
}) {
  return (
    <div className="bg-surface-container-lowest rounded-xl p-space-md shadow-sm flex flex-col justify-between">
      <span className="font-caption text-caption text-outline uppercase tracking-wider">{label}</span>
      <div className="flex items-baseline gap-2 mt-2">
        <span className="font-headline-md text-headline-md text-on-surface font-bold">{value}</span>
        <span className={`font-mono-data text-mono-data flex items-center font-semibold ${metaClass}`}>
          {metaIcon ? <Icon name={metaIcon} className="text-[14px]" /> : null}
          {meta}
        </span>
      </div>
      <div className="w-full bg-surface-container-high h-1.5 rounded-full mt-3 overflow-hidden">
        <div className={`${barClass} h-full ${barWidth}`} />
      </div>
    </div>
  )
}

export function SolutionsPage() {
  const [domain, setDomain] = useState<'marine' | 'aviation' | 'industrial'>('marine')

  const domainClass = (id: typeof domain) =>
    `py-2 px-3 rounded-lg font-label-sm text-label-sm flex items-center justify-center gap-1.5 transition-all border-0 ${
      domain === id
        ? 'bg-primary text-on-primary'
        : 'bg-surface-container-lowest text-on-surface hover:bg-surface-container'
    }`

  return (
    <MarketingFrame>
      <main className="w-full pt-20 bg-background">
        <div className="flex flex-col w-full">
          <div className="relative w-full overflow-hidden">
            <div className="absolute -top-32 left-1/2 -translate-x-1/2 w-[1100px] h-[480px] bg-gradient-to-b from-primary/10 via-secondary-container/10 to-transparent blur-3xl pointer-events-none -z-10" />
            <section className="max-w-[1360px] mx-auto px-gutter pt-space-lg pb-space-xl">
              <div className="flex flex-col items-start gap-space-sm mb-space-md">
                <div className="inline-flex items-center gap-2 bg-secondary/10 text-secondary px-space-md py-1 rounded-full text-label-sm font-label-sm uppercase tracking-wider">
                  <Icon name="tune" className="text-[15px]" fill />
                  Tailored Operational Workflows
                </div>
                <h1 className="font-display-hero text-display-hero text-on-surface tracking-tight max-w-4xl mt-space-xs m-0">
                  Solutions for every operation
                </h1>
                <p className="font-body-lg text-body-lg text-on-surface-variant max-w-3xl mt-space-xs m-0">
                  Whether managing global shipping fleets, high-throughput manufacturing plants, or mission-critical aviation fleets, PartFinder delivers bespoke operational intelligence to eliminate downtime.
                </p>
                <Link
                  to="/products"
                  className="mt-space-md inline-flex items-center gap-1.5 bg-primary text-on-primary font-label-md text-label-md px-space-md py-space-sm rounded-lg hover:bg-primary-container transition-colors shadow-sm"
                >
                  Browse PartFinder products
                  <Icon name="arrow_forward" className="text-[18px]" />
                </Link>
              </div>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-gutter mt-space-lg">
                <KpiCard
                  label="Active Fleets Monitored"
                  value="14,820"
                  meta="+12%"
                  metaIcon="arrow_upward"
                  metaClass="text-secondary"
                  barClass="bg-primary"
                  barWidth="w-[78%]"
                />
                <KpiCard
                  label="Parts Cross-Referenced"
                  value="48.2M"
                  meta="100% OEM"
                  metaIcon="verified"
                  metaClass="text-secondary"
                  barClass="bg-secondary"
                  barWidth="w-[94%]"
                />
                <KpiCard
                  label="Mean RFQ Turnaround"
                  value="18 min"
                  meta="-74% SLA"
                  metaClass="text-primary"
                  barClass="bg-primary-container"
                  barWidth="w-[65%]"
                />
                <KpiCard
                  label="Supply Chain Redundancy"
                  value="99.98%"
                  meta="Tier 1"
                  metaClass="text-secondary"
                  barClass="bg-secondary-container"
                  barWidth="w-[99%]"
                />
              </div>
            </section>

            <section id="solutions" className="max-w-[1360px] mx-auto px-gutter py-space-xl">
              <div className="flex flex-col md:flex-row md:items-end justify-between mb-space-lg">
                <div>
                  <span className="font-caption text-caption text-primary uppercase font-bold tracking-widest">
                    Industry Deployments
                  </span>
                  <h2 className="font-headline-xl text-headline-xl text-on-surface tracking-tight mt-1 m-0">
                    Engineered for Precision at Scale
                  </h2>
                </div>
                <p className="font-body-sm text-body-sm text-on-surface-variant max-w-md mt-2 md:mt-0 m-0">
                  Tailored algorithms configure data taxonomy, compliance audits, and delivery pathways specifically for your vertical&apos;s regulatory demands.
                </p>
              </div>
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-gutter-lg">
                <article className="group bg-surface-container-lowest rounded-xl overflow-hidden shadow-sm hover:shadow-xl transition-all duration-300 flex flex-col">
                  <div className="p-space-lg flex flex-col flex-grow">
                    <div className="flex items-start justify-between gap-space-md mb-space-md">
                      <div className="flex items-center gap-3">
                        <div className="w-12 h-12 rounded-lg bg-surface-container-high flex items-center justify-center text-primary group-hover:bg-primary group-hover:text-on-primary transition-colors flex-shrink-0">
                          <Icon name="directions_boat" className="text-[28px]" />
                        </div>
                        <div>
                          <div className="flex items-center gap-2 mb-1 flex-wrap">
                            <span className="font-mono-data text-caption text-primary font-bold tracking-wider uppercase bg-primary/10 px-2 py-0.5 rounded">
                              SYS-SOL-01
                            </span>
                            <span className="font-mono-data text-caption text-secondary font-semibold">
                              Naval &amp; Global Shipping
                            </span>
                          </div>
                          <h3 className="font-headline-md text-headline-md text-on-surface font-extrabold tracking-tight m-0">
                            Fleet &amp; Vessel Management
                          </h3>
                        </div>
                      </div>
                      <div className="bg-surface-container-low px-space-sm py-1.5 rounded-lg text-right flex-shrink-0 shadow-sm">
                        <span className="font-caption text-caption text-outline block uppercase tracking-wider font-semibold">
                          Vessel Impact
                        </span>
                        <span className="font-headline-sm text-headline-sm text-primary font-extrabold tracking-tight block">
                          42% Faster
                        </span>
                        <span className="font-caption text-caption text-on-surface-variant block font-medium">
                          Port Turnaround
                        </span>
                      </div>
                    </div>
                    <p className="font-body-md text-body-md text-on-surface-variant mb-space-md m-0">
                      Synchronise marine shipping lines, offshore exploration rigs, and drydock repair sequences with strict automated IMPA and ISSA catalogue matching and bonded bunker delivery routes.
                    </p>
                    <div className="flex flex-wrap gap-1.5 mb-space-md">
                      <Pill>IMPA / ISSA Codes</Pill>
                      <Pill>Drydock Engine</Pill>
                      <Pill>Bonded Bunker</Pill>
                      <Pill>Customs Logistics</Pill>
                    </div>
                    <div className="bg-surface-container-low rounded-lg p-space-md my-auto flex flex-col gap-2 shadow-sm">
                      <div className="flex items-center justify-between font-mono-data text-mono-data pb-1 gap-2">
                        <span className="flex items-center gap-1.5 font-bold text-on-surface">
                          <span className="w-2 h-2 rounded-full bg-secondary animate-pulse" />
                          MV Atlantic Voyager
                        </span>
                        <span className="bg-secondary/10 text-secondary font-mono-data text-caption px-2 py-0.5 rounded font-bold">
                          IMO: 9814422
                        </span>
                      </div>
                      <div className="grid grid-cols-3 gap-2 pt-1 text-center">
                        <div className="bg-surface-container-lowest p-2 rounded shadow-sm">
                          <span className="font-caption text-caption text-outline block font-semibold uppercase">Catalogue</span>
                          <span className="font-mono-data text-mono-data font-bold text-on-surface">75-VALVES</span>
                        </div>
                        <div className="bg-surface-container-lowest p-2 rounded shadow-sm">
                          <span className="font-caption text-caption text-outline block font-semibold uppercase">Clearance</span>
                          <span className="font-mono-data text-mono-data font-bold text-primary">RTM ETA 14h</span>
                        </div>
                        <div className="bg-surface-container-lowest p-2 rounded shadow-sm">
                          <span className="font-caption text-caption text-outline block font-semibold uppercase">Manifest</span>
                          <span className="font-mono-data text-mono-data font-bold text-secondary">99.4% Ready</span>
                        </div>
                      </div>
                    </div>
                    <div className="mt-space-md rounded-lg overflow-hidden h-44 relative">
                      <img
                        className="w-full h-full object-cover block"
                        alt="High-resolution industrial aerial photo of a massive deep-sea container ship docked at Rotterdam port drydock terminal."
                        src={SHIP_IMAGE}
                      />
                      <div className="absolute inset-0 bg-gradient-to-t from-inverse-surface/80 via-transparent to-transparent flex items-end p-3">
                        <span className="font-mono-data text-mono-data text-inverse-on-surface font-semibold flex items-center gap-1.5">
                          <span className="w-2 h-2 rounded-full bg-secondary" />
                          Automated Voyage Dispatch Hub • Live Telemetry
                        </span>
                      </div>
                    </div>
                    <div className="pt-space-md mt-space-md flex items-center justify-between gap-3 flex-wrap">
                      <span className="font-mono-data text-caption text-outline flex items-center gap-1">
                        <Icon name="cloud_done" className="text-[15px]" /> Cloud / Air-Gapped Satellite
                      </span>
                      <ExploreLink />
                    </div>
                  </div>
                </article>

                <article className="group bg-surface-container-lowest rounded-xl overflow-hidden shadow-sm hover:shadow-xl transition-all duration-300 flex flex-col">
                  <div className="p-space-lg flex flex-col flex-grow">
                    <div className="flex items-start justify-between gap-space-md mb-space-md">
                      <div className="flex items-center gap-3">
                        <div className="w-12 h-12 rounded-lg bg-surface-container-high flex items-center justify-center text-primary group-hover:bg-primary group-hover:text-on-primary transition-colors flex-shrink-0">
                          <Icon name="build" className="text-[28px]" />
                        </div>
                        <div>
                          <div className="flex items-center gap-2 mb-1 flex-wrap">
                            <span className="font-mono-data text-caption text-primary font-bold tracking-wider uppercase bg-primary/10 px-2 py-0.5 rounded">
                              SYS-SOL-02
                            </span>
                            <span className="font-mono-data text-caption text-secondary font-semibold">
                              Plant Floor &amp; Heavy Field MRO
                            </span>
                          </div>
                          <h3 className="font-headline-md text-headline-md text-on-surface font-extrabold tracking-tight m-0">
                            Maintenance &amp; Repair Operations
                          </h3>
                        </div>
                      </div>
                      <div className="bg-surface-container-low px-space-sm py-1.5 rounded-lg text-right flex-shrink-0 shadow-sm">
                        <span className="font-caption text-caption text-outline block uppercase tracking-wider font-semibold">
                          Uptime Yield
                        </span>
                        <span className="font-headline-sm text-headline-sm text-primary font-extrabold tracking-tight block">
                          68% Less
                        </span>
                        <span className="font-caption text-caption text-on-surface-variant block font-medium">
                          Unscheduled Downtime
                        </span>
                      </div>
                    </div>
                    <p className="font-body-md text-body-md text-on-surface-variant mb-space-md m-0">
                      Equip plant engineers and certified technicians with instant mobile field barcode identification, preventative maintenance bill-of-materials kitting, and rapid machine stoppage triage.
                    </p>
                    <div className="flex flex-wrap gap-1.5 mb-space-md">
                      <Pill>Preventative Kitting</Pill>
                      <Pill>Triage Engine</Pill>
                      <Pill>Offline OCR</Pill>
                      <Pill>SAP PM / Maximo</Pill>
                    </div>
                    <div className="bg-surface-container-low rounded-lg p-space-md my-auto flex flex-col gap-2 shadow-sm">
                      <div className="flex items-center justify-between font-mono-data text-mono-data pb-1 gap-2">
                        <span className="flex items-center gap-1.5 font-bold text-on-surface">
                          <span className="w-2 h-2 rounded-full bg-primary-container animate-pulse" />
                          Turbine Assembly PM-0881
                        </span>
                        <span className="bg-primary/10 text-primary font-mono-data text-caption px-2 py-0.5 rounded font-bold">
                          Stage 4 Inspection
                        </span>
                      </div>
                      <div className="grid grid-cols-3 gap-2 pt-1 text-center">
                        <div className="bg-surface-container-lowest p-2 rounded shadow-sm">
                          <span className="font-caption text-caption text-outline block font-semibold uppercase">Spares</span>
                          <span className="font-mono-data text-mono-data font-bold text-on-surface">14/14 Staged</span>
                        </div>
                        <div className="bg-surface-container-lowest p-2 rounded shadow-sm">
                          <span className="font-caption text-caption text-outline block font-semibold uppercase">MTTR Delta</span>
                          <span className="font-mono-data text-mono-data font-bold text-secondary">-4.5 Hours</span>
                        </div>
                        <div className="bg-surface-container-lowest p-2 rounded shadow-sm">
                          <span className="font-caption text-caption text-outline block font-semibold uppercase">Technician</span>
                          <span className="font-mono-data text-mono-data font-bold text-on-surface">Active On-Site</span>
                        </div>
                      </div>
                    </div>
                    <div className="mt-space-md rounded-lg overflow-hidden h-44 relative">
                      <img
                        className="w-full h-full object-cover block"
                        alt="Industrial maintenance technician inspecting a precision turbine bearing housing with a rugged tablet."
                        src={TECH_IMAGE}
                      />
                      <div className="absolute inset-0 bg-gradient-to-t from-inverse-surface/80 via-transparent to-transparent flex items-end p-3">
                        <span className="font-mono-data text-mono-data text-inverse-on-surface font-semibold flex items-center gap-1.5">
                          <span className="w-2 h-2 rounded-full bg-primary" />
                          Smart Assembly Kit Verification • Factory Floor
                        </span>
                      </div>
                    </div>
                    <div className="pt-space-md mt-space-md flex items-center justify-between gap-3 flex-wrap">
                      <span className="font-mono-data text-caption text-outline flex items-center gap-1">
                        <Icon name="sync" className="text-[15px]" /> Integrations: SAP, Maximo, Infor
                      </span>
                      <ExploreLink />
                    </div>
                  </div>
                </article>

                <article className="group bg-surface-container-lowest rounded-xl overflow-hidden shadow-sm hover:shadow-xl transition-all duration-300 flex flex-col">
                  <div className="p-space-lg flex flex-col flex-grow">
                    <div className="flex items-start justify-between gap-space-md mb-space-md">
                      <div className="flex items-center gap-3">
                        <div className="w-12 h-12 rounded-lg bg-surface-container-high flex items-center justify-center text-primary group-hover:bg-primary group-hover:text-on-primary transition-colors flex-shrink-0">
                          <Icon name="trending_down" className="text-[28px]" />
                        </div>
                        <div>
                          <div className="flex items-center gap-2 mb-1 flex-wrap">
                            <span className="font-mono-data text-caption text-primary font-bold tracking-wider uppercase bg-primary/10 px-2 py-0.5 rounded">
                              SYS-SOL-03
                            </span>
                            <span className="font-mono-data text-caption text-secondary font-semibold">
                              Global Strategic Sourcing
                            </span>
                          </div>
                          <h3 className="font-headline-md text-headline-md text-on-surface font-extrabold tracking-tight m-0">
                            Procurement &amp; Supply Chain
                          </h3>
                        </div>
                      </div>
                      <div className="bg-surface-container-low px-space-sm py-1.5 rounded-lg text-right flex-shrink-0 shadow-sm">
                        <span className="font-caption text-caption text-outline block uppercase tracking-wider font-semibold">
                          Cost Optimisation
                        </span>
                        <span className="font-headline-sm text-headline-sm text-primary font-extrabold tracking-tight block">
                          18.5% Net
                        </span>
                        <span className="font-caption text-caption text-on-surface-variant block font-medium">
                          Direct Spend Drop
                        </span>
                      </div>
                    </div>
                    <p className="font-body-md text-body-md text-on-surface-variant mb-space-md m-0">
                      Bypass fragmented broker margins. Leverage direct-to-plant sourcing contracts, automated bulk consolidated freight, and dynamic real-time price indexing across 1,000+ audited enterprise suppliers.
                    </p>
                    <div className="flex flex-wrap gap-1.5 mb-space-md">
                      <Pill>Direct-to-Plant Routing</Pill>
                      <Pill>Consolidated Freight</Pill>
                      <Pill>1,000+ Audited Vendors</Pill>
                      <Pill>Margin Arbitrage</Pill>
                    </div>
                    <div className="bg-surface-container-low rounded-lg p-space-md my-auto flex flex-col gap-2 shadow-sm">
                      <div className="flex items-center justify-between font-mono-data text-mono-data pb-1 gap-2">
                        <span className="flex items-center gap-1.5 font-bold text-on-surface">
                          <span className="w-2 h-2 rounded-full bg-secondary animate-pulse" />
                          PO Batch #RFQ-2025-993
                        </span>
                        <span className="bg-secondary/10 text-secondary font-mono-data text-caption px-2 py-0.5 rounded font-bold">
                          Saved: $42,910
                        </span>
                      </div>
                      <div className="flex items-center justify-between gap-4 py-2">
                        <div className="flex-1 bg-surface-container-lowest p-2 rounded shadow-sm">
                          <div className="flex justify-between font-caption text-caption text-outline mb-1 font-semibold uppercase">
                            <span>Broker Base</span>
                            <span>$184,200</span>
                          </div>
                          <div className="w-full bg-surface-container-high h-2 rounded-full overflow-hidden">
                            <div className="bg-tertiary-container h-full w-[85%]" />
                          </div>
                        </div>
                        <div className="flex-1 bg-surface-container-lowest p-2 rounded shadow-sm">
                          <div className="flex justify-between font-caption text-caption text-primary font-bold mb-1 uppercase">
                            <span>PartFinder Direct</span>
                            <span>$141,290</span>
                          </div>
                          <div className="w-full bg-surface-container-high h-2 rounded-full overflow-hidden">
                            <div className="bg-primary h-full w-[65%]" />
                          </div>
                        </div>
                      </div>
                    </div>
                    <div className="pt-space-md mt-space-md flex items-center justify-between gap-3 flex-wrap">
                      <span className="font-mono-data text-caption text-outline flex items-center gap-1">
                        <Icon name="api" className="text-[15px]" /> PunchOut Catalogue L2 • cXML Native
                      </span>
                      <ExploreLink />
                    </div>
                  </div>
                </article>

                <article className="group bg-surface-container-lowest rounded-xl overflow-hidden shadow-sm hover:shadow-xl transition-all duration-300 flex flex-col">
                  <div className="p-space-lg flex flex-col flex-grow">
                    <div className="flex items-start justify-between gap-space-md mb-space-md">
                      <div className="flex items-center gap-3">
                        <div className="w-12 h-12 rounded-lg bg-surface-container-high flex items-center justify-center text-primary group-hover:bg-primary group-hover:text-on-primary transition-colors flex-shrink-0">
                          <Icon name="verified_user" className="text-[28px]" />
                        </div>
                        <div>
                          <div className="flex items-center gap-2 mb-1 flex-wrap">
                            <span className="font-mono-data text-caption text-primary font-bold tracking-wider uppercase bg-primary/10 px-2 py-0.5 rounded">
                              SYS-SOL-04
                            </span>
                            <span className="font-mono-data text-caption text-secondary font-semibold">
                              Aviation &amp; Defense Reliability
                            </span>
                          </div>
                          <h3 className="font-headline-md text-headline-md text-on-surface font-extrabold tracking-tight m-0">
                            OEM &amp; Aftermarket Support
                          </h3>
                        </div>
                      </div>
                      <div className="bg-surface-container-low px-space-sm py-1.5 rounded-lg text-right flex-shrink-0 shadow-sm">
                        <span className="font-caption text-caption text-outline block uppercase tracking-wider font-semibold">
                          Integrity Score
                        </span>
                        <span className="font-headline-sm text-headline-sm text-primary font-extrabold tracking-tight block">
                          100%
                        </span>
                        <span className="font-caption text-caption text-on-surface-variant block font-medium">
                          Verified Traceability
                        </span>
                      </div>
                    </div>
                    <p className="font-body-md text-body-md text-on-surface-variant mb-space-md m-0">
                      Validate original manufacturer warranties, cross-reference high-grade certified alternatives with engineering tolerance matching, and deploy algorithmic counterfeit screening before components arrive.
                    </p>
                    <div className="flex flex-wrap gap-1.5 mb-space-md">
                      <Pill>Warranty Validation Gate</Pill>
                      <Pill>FAA / EASA Conformity</Pill>
                      <Pill>Counterfeit AI</Pill>
                      <Pill>Form 8130-3 Tagging</Pill>
                    </div>
                    <div className="bg-surface-container-low rounded-lg p-space-md my-auto flex flex-col gap-2 shadow-sm">
                      <div className="flex items-center justify-between font-mono-data text-mono-data pb-1 gap-2">
                        <span className="flex items-center gap-1.5 font-bold text-on-surface">
                          <span className="w-2 h-2 rounded-full bg-secondary animate-pulse" />
                          Avionics Hydraulic Actuator
                        </span>
                        <span className="text-outline font-mono-data text-caption font-semibold">NSN: 5935-01-442</span>
                      </div>
                      <div className="flex items-center justify-between bg-surface-container-lowest p-2 rounded shadow-sm gap-2">
                        <div className="flex items-center gap-2">
                          <Icon name="verified" className="text-secondary text-[20px]" />
                          <span className="font-body-sm text-body-sm font-semibold text-on-surface">
                            OEM Certificate of Conformance #99831
                          </span>
                        </div>
                        <span className="font-mono-data text-caption bg-secondary-fixed text-on-secondary-fixed-variant px-2 py-0.5 rounded font-bold uppercase tracking-wider whitespace-nowrap">
                          Crypto-Signed
                        </span>
                      </div>
                    </div>
                    <div className="pt-space-md mt-space-md flex items-center justify-between gap-3 flex-wrap">
                      <span className="font-mono-data text-caption text-outline flex items-center gap-1">
                        <Icon name="gavel" className="text-[15px]" /> ITAR &amp; Dual-Use Compliant
                      </span>
                      <ExploreLink />
                    </div>
                  </div>
                </article>

                <article className="group bg-surface-container-lowest rounded-xl overflow-hidden shadow-sm hover:shadow-xl transition-all duration-300 flex flex-col">
                  <div className="p-space-lg flex flex-col flex-grow">
                    <div className="flex items-start justify-between gap-space-md mb-space-md">
                      <div className="flex items-center gap-3">
                        <div className="w-12 h-12 rounded-lg bg-surface-container-high flex items-center justify-center text-primary group-hover:bg-primary group-hover:text-on-primary transition-colors flex-shrink-0">
                          <Icon name="corporate_fare" className="text-[28px]" />
                        </div>
                        <div>
                          <div className="flex items-center gap-2 mb-1 flex-wrap">
                            <span className="font-mono-data text-caption text-primary font-bold tracking-wider uppercase bg-primary/10 px-2 py-0.5 rounded">
                              SYS-SOL-05
                            </span>
                            <span className="font-mono-data text-caption text-secondary font-semibold">
                              Global Conglomerate Control
                            </span>
                          </div>
                          <h3 className="font-headline-md text-headline-md text-on-surface font-extrabold tracking-tight m-0">
                            Multi-Organisation Governance
                          </h3>
                        </div>
                      </div>
                      <div className="bg-surface-container-low px-space-sm py-1.5 rounded-lg text-right flex-shrink-0 shadow-sm">
                        <span className="font-caption text-caption text-outline block uppercase tracking-wider font-semibold">
                          Entity Scalability
                        </span>
                        <span className="font-headline-sm text-headline-sm text-primary font-extrabold tracking-tight block">
                          50+ Plants
                        </span>
                        <span className="font-caption text-caption text-on-surface-variant block font-medium">
                          Unified Administration
                        </span>
                      </div>
                    </div>
                    <p className="font-body-md text-body-md text-on-surface-variant mb-space-md m-0">
                      Deliver airtight data isolation across subsidiary holdings, multi-tier spending approvals, real-time localized currency reconciliation, and automatic VAT and customs duty recalculations.
                    </p>
                    <div className="flex flex-wrap gap-1.5 mb-space-md">
                      <Pill>Multi-Tenant Partitioning</Pill>
                      <Pill>Hierarchy Approval</Pill>
                      <Pill>Cross-Border Tariffs</Pill>
                      <Pill>SSO &amp; RBAC</Pill>
                    </div>
                    <div className="bg-surface-container-low rounded-lg p-space-md my-auto flex flex-col gap-2 shadow-sm">
                      <div className="flex items-center justify-between font-mono-data text-mono-data pb-1 gap-2">
                        <span className="text-on-surface font-bold flex items-center gap-1.5">
                          <span className="w-2 h-2 rounded-full bg-primary" /> Subsidiary Partitioning
                        </span>
                        <span className="text-primary font-mono-data text-caption font-bold bg-primary/10 px-2 py-0.5 rounded">
                          Active: 6 Regions
                        </span>
                      </div>
                      <div className="space-y-1.5">
                        <div className="flex items-center justify-between text-caption font-mono-data bg-surface-container-lowest p-2 rounded shadow-sm gap-2">
                          <span className="text-on-surface font-medium">Nordic Maritime Division (DK)</span>
                          <span className="text-secondary font-bold whitespace-nowrap">DKK / EUR Compliant</span>
                        </div>
                        <div className="flex items-center justify-between text-caption font-mono-data bg-surface-container-lowest p-2 rounded shadow-sm gap-2">
                          <span className="text-on-surface font-medium">APAC Marine Offshore (SG)</span>
                          <span className="text-secondary font-bold whitespace-nowrap">GST Exemption Passed</span>
                        </div>
                      </div>
                    </div>
                    <div className="pt-space-md mt-space-md flex items-center justify-between gap-3 flex-wrap">
                      <span className="font-mono-data text-caption text-outline flex items-center gap-1">
                        <Icon name="security" className="text-[15px]" /> SOC 2 Type II • FedRAMP Moderate
                      </span>
                      <ExploreLink />
                    </div>
                  </div>
                </article>

                <article className="group bg-surface-container-lowest rounded-xl overflow-hidden shadow-sm hover:shadow-xl transition-all duration-300 flex flex-col">
                  <div className="p-space-lg flex flex-col flex-grow">
                    <div className="flex items-start justify-between gap-space-md mb-space-md">
                      <div className="flex items-center gap-3">
                        <div className="w-12 h-12 rounded-lg bg-surface-container-high flex items-center justify-center text-primary group-hover:bg-primary group-hover:text-on-primary transition-colors flex-shrink-0">
                          <Icon name="query_stats" className="text-[28px]" />
                        </div>
                        <div>
                          <div className="flex items-center gap-2 mb-1 flex-wrap">
                            <span className="font-mono-data text-caption text-primary font-bold tracking-wider uppercase bg-primary/10 px-2 py-0.5 rounded">
                              SYS-SOL-06
                            </span>
                            <span className="font-mono-data text-caption text-secondary font-semibold">
                              Working Capital Reclamation
                            </span>
                          </div>
                          <h3 className="font-headline-md text-headline-md text-on-surface font-extrabold tracking-tight m-0">
                            Data &amp; Predictive Analytics
                          </h3>
                        </div>
                      </div>
                      <div className="bg-surface-container-low px-space-sm py-1.5 rounded-lg text-right flex-shrink-0 shadow-sm">
                        <span className="font-caption text-caption text-outline block uppercase tracking-wider font-semibold">
                          Capital Unlocked
                        </span>
                        <span className="font-headline-sm text-headline-sm text-primary font-extrabold tracking-tight block">
                          $3.4M Avg
                        </span>
                        <span className="font-caption text-caption text-on-surface-variant block font-medium">
                          Released Idle Capital
                        </span>
                      </div>
                    </div>
                    <p className="font-body-md text-body-md text-on-surface-variant mb-space-md m-0">
                      Harness Mean Time Between Failures (MTBF) predictive forecasting to stage critical parts just-in-time, liquidate inactive dead stock, and systematically eliminate factory obsolescence costs.
                    </p>
                    <div className="flex flex-wrap gap-1.5 mb-space-md">
                      <Pill>MTBF Predictor</Pill>
                      <Pill>Dead-Stock Exchange</Pill>
                      <Pill>Obsolescence Alerts</Pill>
                      <Pill>Dynamic Thresholds</Pill>
                    </div>
                    <div className="bg-surface-container-low rounded-lg p-space-md my-auto flex flex-col gap-2 shadow-sm">
                      <div className="flex items-center justify-between font-mono-data text-mono-data pb-1 gap-2">
                        <span className="flex items-center gap-1.5 font-bold text-on-surface">
                          <span className="w-2 h-2 rounded-full bg-primary animate-pulse" />
                          Forecast: High-Pressure Seal #48
                        </span>
                        <span className="text-error font-bold font-mono-data text-caption bg-error/10 px-2 py-0.5 rounded">
                          Failure in ~480h
                        </span>
                      </div>
                      <div className="bg-surface-container-lowest p-2 rounded flex items-center justify-between shadow-sm gap-2">
                        <div>
                          <span className="font-caption text-caption text-outline block font-semibold uppercase">
                            Automated Action
                          </span>
                          <span className="font-body-sm text-body-sm font-semibold text-on-surface">
                            Dispatch from Antwerp Hub
                          </span>
                        </div>
                        <span className="font-mono-data text-caption text-primary bg-primary-fixed px-2 py-1 rounded font-bold uppercase tracking-wider whitespace-nowrap">
                          SLA: 36h Ahead
                        </span>
                      </div>
                    </div>
                    <div className="pt-space-md mt-space-md flex items-center justify-between gap-3 flex-wrap">
                      <span className="font-mono-data text-caption text-outline flex items-center gap-1">
                        <Icon name="memory" className="text-[15px]" /> ML Engine: MTBF-v4.8
                      </span>
                      <ExploreLink />
                    </div>
                  </div>
                </article>
              </div>
            </section>

            <section id="impact" className="max-w-[1360px] mx-auto px-gutter py-space-xl">
              <div className="bg-inverse-surface text-inverse-on-surface rounded-xl p-space-lg md:p-space-xl relative overflow-hidden">
                <div className="absolute top-0 right-0 w-[500px] h-[500px] bg-gradient-to-br from-primary/30 to-secondary/10 rounded-full blur-3xl pointer-events-none -mr-20 -mt-20" />
                <div className="relative z-10 flex flex-col gap-space-lg">
                  <div className="flex flex-col md:flex-row md:items-end justify-between gap-space-md">
                    <div>
                      <span className="font-caption text-caption text-secondary-fixed uppercase tracking-widest font-bold">
                        Verified Enterprise Metrics
                      </span>
                      <h2 className="font-headline-xl text-headline-xl text-surface-bright tracking-tight mt-1 m-0">
                        Measurable Impact Across Heavy Industry
                      </h2>
                    </div>
                    <div className="flex items-center gap-2 bg-on-secondary-fixed-variant px-space-md py-1.5 rounded-full text-secondary-fixed font-mono-data text-mono-data w-fit">
                      <Icon name="verified" className="text-[16px]" />
                      Third-Party Audited Findings
                    </div>
                  </div>
                  <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-gutter pt-space-md">
                    <div className="bg-surface-variant/10 rounded-xl p-space-md backdrop-blur-md flex flex-col justify-between">
                      <span className="font-caption text-caption text-surface-variant uppercase font-semibold">Reliability Gain</span>
                      <div className="my-space-sm">
                        <span className="font-display-hero-mobile text-display-hero-mobile text-surface-bright font-extrabold tracking-tight">
                          45%
                        </span>
                        <span className="font-headline-sm text-headline-sm text-secondary-fixed block font-bold">
                          Downtime Reduction
                        </span>
                      </div>
                      <p className="font-caption text-caption text-outline-variant m-0">
                        Across 1,200+ container vessels and continuous-flow chemical refining units.
                      </p>
                    </div>
                    <div className="bg-surface-variant/10 rounded-xl p-space-md backdrop-blur-md flex flex-col justify-between">
                      <span className="font-caption text-caption text-surface-variant uppercase font-semibold">Procurement Velocity</span>
                      <div className="my-space-sm">
                        <span className="font-display-hero-mobile text-display-hero-mobile text-surface-bright font-extrabold tracking-tight">
                          3.2x
                        </span>
                        <span className="font-headline-sm text-headline-sm text-secondary-fixed block font-bold">
                          Faster Sourcing Cycle
                        </span>
                      </div>
                      <p className="font-caption text-caption text-outline-variant m-0">
                        From technical RFQ creation to authorised dockside/plant gate receipt.
                      </p>
                    </div>
                    <div className="bg-surface-variant/10 rounded-xl p-space-md backdrop-blur-md flex flex-col justify-between">
                      <span className="font-caption text-caption text-surface-variant uppercase font-semibold">Cost Efficiency</span>
                      <div className="my-space-sm">
                        <span className="font-display-hero-mobile text-display-hero-mobile text-surface-bright font-extrabold tracking-tight">
                          22%
                        </span>
                        <span className="font-headline-sm text-headline-sm text-secondary-fixed block font-bold">
                          Critical Component Savings
                        </span>
                      </div>
                      <p className="font-caption text-caption text-outline-variant m-0">
                        Eliminating intermediate broker arbitrage on specialized OEM fabrications.
                      </p>
                    </div>
                    <div className="bg-surface-variant/10 rounded-xl p-space-md backdrop-blur-md flex flex-col justify-between">
                      <span className="font-caption text-caption text-surface-variant uppercase font-semibold">Logistical Precision</span>
                      <div className="my-space-sm">
                        <span className="font-display-hero-mobile text-display-hero-mobile text-surface-bright font-extrabold tracking-tight">
                          99.8%
                        </span>
                        <span className="font-headline-sm text-headline-sm text-secondary-fixed block font-bold">
                          On-Time Port/Plant Delivery
                        </span>
                      </div>
                      <p className="font-caption text-caption text-outline-variant m-0">
                        Ensuring bonded port berths or factory shutdown shifts are never missed.
                      </p>
                    </div>
                  </div>
                  <div className="mt-space-md bg-surface-variant/10 rounded-xl p-space-lg backdrop-blur-md flex flex-col lg:flex-row items-center gap-space-lg">
                    <div className="w-full lg:w-2/5 rounded-lg overflow-hidden h-56 relative flex-shrink-0">
                      <img
                        className="w-full h-full object-cover block"
                        alt="Ultra-large commercial container vessel crossing open waters under crisp blue skies."
                        src={CASE_IMAGE}
                      />
                      <div className="absolute top-3 left-3 bg-inverse-surface/80 px-2.5 py-1 rounded text-caption font-mono-data text-secondary-fixed">
                        CASE #TR-0894
                      </div>
                    </div>
                    <div className="w-full lg:w-3/5 flex flex-col gap-space-sm">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="font-headline-sm text-headline-sm font-bold text-surface-bright">
                          TransOcean Shipping Lines
                        </span>
                        <span className="bg-secondary-fixed text-on-secondary-fixed-variant font-caption text-caption font-bold px-2 py-0.5 rounded">
                          FLEET: 84 VESSELS
                        </span>
                      </div>
                      <blockquote className="font-body-lg text-body-lg text-surface-container font-medium italic m-0">
                        “How TransOcean Shipping reduced vessel spares lead time by 14 days using PartFinder automated sourcing.”
                      </blockquote>
                      <p className="font-body-sm text-body-sm text-outline-variant m-0">
                        Faced with escalating drydock surcharges in Singapore and Rotterdam, TransOcean unified its onboard procurement with PartFinder&apos;s IMPA-indexed automated RFQ pipeline. Emergency spares delivery lead-times collapsed from 22 days to under 8 days.
                      </p>
                      <div className="flex flex-wrap items-center gap-space-md pt-space-xs">
                        <div className="flex items-center gap-1.5 text-secondary-fixed font-mono-data text-mono-data font-semibold">
                          <Icon name="schedule" className="text-[18px]" />
                          <span>14 Days Saved per Refit</span>
                        </div>
                        <div className="flex items-center gap-1.5 text-secondary-fixed font-mono-data text-mono-data font-semibold">
                          <Icon name="payments" className="text-[18px]" />
                          <span>$1.85M Annual Berth Fees Avoided</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </section>

            <section id="architect" className="max-w-[1360px] mx-auto px-gutter py-space-xl">
              <div className="bg-surface-container-lowest rounded-xl p-space-lg md:p-space-xl shadow-lg relative overflow-hidden">
                <div className="grid grid-cols-1 lg:grid-cols-12 gap-gutter-lg items-center">
                  <div className="lg:col-span-6 flex flex-col gap-space-sm">
                    <span className="font-caption text-caption text-primary uppercase font-bold tracking-widest">
                      Architectural Advisory
                    </span>
                    <h2 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight m-0">
                      Consult with our Industry Solutions Architects
                    </h2>
                    <p className="font-body-md text-body-md text-on-surface-variant m-0">
                      Every enterprise deployment receives a dedicated Lead Systems Engineer. We analyse your ERP configuration, active parts taxonomy, and operational risk factors to draft an accelerated transition blueprint.
                    </p>
                    <div className="flex flex-col gap-2 mt-space-sm">
                      <div className="flex items-center gap-2">
                        <Icon name="check_circle" className="text-primary text-[20px]" />
                        <span className="font-body-sm text-body-sm text-on-surface font-semibold">
                          Custom ERP &amp; CMMS API architecture roadmap
                        </span>
                      </div>
                      <div className="flex items-center gap-2">
                        <Icon name="check_circle" className="text-primary text-[20px]" />
                        <span className="font-body-sm text-body-sm text-on-surface font-semibold">
                          Complimentary historical parts taxonomy scrub &amp; MTBF audit
                        </span>
                      </div>
                      <div className="flex items-center gap-2">
                        <Icon name="check_circle" className="text-primary text-[20px]" />
                        <span className="font-body-sm text-body-sm text-on-surface font-semibold">
                          On-site technical stakeholder consultation
                        </span>
                      </div>
                    </div>
                  </div>
                  <form
                    className="lg:col-span-6 bg-surface-container-low rounded-xl p-space-lg shadow-sm flex flex-col gap-space-md"
                    onSubmit={(event) => event.preventDefault()}
                  >
                    <div className="flex items-center justify-between pb-space-xs">
                      <span className="font-label-md text-label-md font-bold text-on-surface">
                        Configure Operational Profile
                      </span>
                      <span className="font-mono-data text-mono-data text-primary font-bold">Step 1 of 2</span>
                    </div>
                    <div>
                      <label className="font-label-sm text-label-sm text-on-surface block mb-1.5">
                        Select Primary Operating Domain
                      </label>
                      <div className="grid grid-cols-3 gap-2">
                        <button className={domainClass('marine')} type="button" onClick={() => setDomain('marine')}>
                          <Icon name="directions_boat" className="text-[16px]" /> Marine
                        </button>
                        <button className={domainClass('aviation')} type="button" onClick={() => setDomain('aviation')}>
                          <Icon name="flight" className="text-[16px]" /> Aviation
                        </button>
                        <button className={domainClass('industrial')} type="button" onClick={() => setDomain('industrial')}>
                          <Icon name="factory" className="text-[16px]" /> Plants
                        </button>
                      </div>
                    </div>
                    <div>
                      <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="fleet-size">
                        Asset Footprint / Fleet Size
                      </label>
                      <select
                        id="fleet-size"
                        className="w-full h-10 px-3 bg-surface-container-lowest rounded-lg font-body-sm text-body-sm text-on-surface focus:outline-none focus:ring-2 focus:ring-primary border-0"
                      >
                        <option>10 – 50 Active Units / Production Lines</option>
                        <option>51 – 250 Active Units / Global Vessels</option>
                        <option>250+ Multi-Site Enterprise Fleet</option>
                      </select>
                    </div>
                    <div>
                      <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="architect-email">
                        Corporate Email for Engineering Dossier
                      </label>
                      <div className="flex flex-col sm:flex-row gap-2">
                        <input
                          id="architect-email"
                          className="flex-grow h-11 px-3 bg-surface-container-lowest rounded-lg font-body-sm text-body-sm text-on-surface placeholder:text-outline focus:outline-none focus:ring-2 focus:ring-primary border-0"
                          placeholder="chief.engineer@enterprise.com"
                          type="email"
                        />
                        <button
                          className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors flex items-center justify-center gap-1.5 shadow-sm whitespace-nowrap border-0"
                          type="submit"
                        >
                          <span>Connect with Architect</span>
                          <Icon name="arrow_forward" className="text-[18px]" />
                        </button>
                      </div>
                    </div>
                    <div className="flex items-center gap-2 pt-1 font-caption text-caption text-outline">
                      <Icon name="lock" className="text-[16px] text-secondary" />
                      <span>Encrypted transmission. NDA standard pre-signed by PartFinder Inc.</span>
                    </div>
                  </form>
                </div>
              </div>
            </section>
          </div>
        </div>
      </main>
    </MarketingFrame>
  )
}
