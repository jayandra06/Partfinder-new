import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { buildApiUrl } from './accountSession'
import { Icon, MarketingFrame } from './SiteChrome'
import { SELF_SERVE_PRICE_LABEL } from './pricing.constants'
import { startLicencePayment, type LicenceCredentials } from './razorpayCheckout'

type CheckoutStatus = {
  ready?: boolean
  enabled?: boolean
  configured?: boolean
  testMode?: boolean | null
  priceLabel?: string
  message?: string | null
}

const fieldClass =
  'w-full h-11 px-3 bg-surface-container-lowest rounded-lg font-body-sm text-body-sm text-on-surface placeholder:text-outline focus:outline-none focus:ring-2 focus:ring-primary border-0'

export function GetLicencePage() {
  const [organisation, setOrganisation] = useState('')
  const [email, setEmail] = useState('')
  const [mobile, setMobile] = useState('')
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const [credentials, setCredentials] = useState<LicenceCredentials | null>(null)
  const [checkoutStatus, setCheckoutStatus] = useState<CheckoutStatus | null>(null)

  useEffect(() => {
    let cancelled = false
    ;(async () => {
      try {
        const response = await fetch(buildApiUrl('/public/license-purchase/status'))
        const data = (await response.json().catch(() => null)) as CheckoutStatus | null
        if (!cancelled && response.ok && data) {
          setCheckoutStatus(data)
        }
      } catch {
        if (!cancelled) setCheckoutStatus({ ready: false, enabled: false, configured: false })
      }
    })()
    return () => {
      cancelled = true
    }
  }, [])

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const name = organisation.trim()
    const mail = email.trim()
    const phone = mobile.trim()
    const digits = phone.replace(/\D/g, '')

    if (!name) {
      setError('Enter the organisation name.')
      return
    }
    if (!mail || !mail.includes('@')) {
      setError('Enter a valid email address.')
      return
    }
    if (digits.length < 8) {
      setError('Enter a valid mobile number.')
      return
    }

    setError('')
    setPending(true)
    try {
      const issued = await startLicencePayment({
        organisationName: name,
        email: mail,
        mobile: phone,
      })
      setCredentials(issued)
    } catch (cause) {
      const message = cause instanceof Error ? cause.message : 'Payment could not be completed.'
      if (message !== 'Payment was cancelled.') {
        setError(message)
      }
    } finally {
      setPending(false)
    }
  }

  return (
    <MarketingFrame>
      <main className="w-full pt-20 bg-background min-h-[calc(100vh-5rem)]">
        <section className="max-w-[1360px] mx-auto px-gutter py-space-lg pb-space-xl">
          <div className="flex items-center gap-space-xs text-primary font-mono-data text-mono-data tracking-widest uppercase bg-surface-container px-space-sm py-1 rounded w-fit mb-space-md">
            <span className="inline-block w-2 h-2 rounded-full bg-primary animate-pulse" />
            <span>LICENSING</span>
          </div>
          <div className="bg-surface-container-lowest rounded-xl p-space-lg md:p-space-xl shadow-lg max-w-2xl">
            <h1 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight m-0">
              Get Licence
            </h1>
            <p className="font-body-md text-body-md text-on-surface-variant mt-space-sm mb-space-lg m-0">
              Pay {SELF_SERVE_PRICE_LABEL} to activate a PartFinder organisation licence. After payment you
              receive your organisation code and temporary admin password.
            </p>

            {credentials ? (
              <div className="flex flex-col gap-space-md">
                <div className="flex items-start gap-2 rounded-lg bg-primary-container/30 text-on-surface px-3 py-2 font-body-sm text-body-sm">
                  <Icon name="check_circle" className="text-[20px] text-primary" />
                  <span>
                    Payment successful for{' '}
                    <strong>{credentials.organisationName}</strong>. Save these credentials — they are
                    also emailed to {credentials.email}.
                  </span>
                </div>
                <div className="bg-surface-container-low rounded-lg p-space-md flex flex-col gap-3">
                  <CredentialRow label="Organisation code" value={credentials.orgCode} mono />
                  <CredentialRow label="Temporary password" value={credentials.temporaryPassword} mono />
                  <CredentialRow label="Admin email" value={credentials.email} />
                </div>
                <p className="font-body-sm text-body-sm text-on-surface-variant m-0">
                  Use these in the PartFinder Windows app setup, or sign in on the web to change your
                  password.
                </p>
                <Link
                  to="/sign-in"
                  className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors inline-flex items-center justify-center gap-1.5 shadow-sm"
                >
                  Sign in with this account
                  <Icon name="arrow_forward" className="text-[18px]" />
                </Link>
                <Link
                  to="/"
                  className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface text-center"
                >
                  Back to home
                </Link>
              </div>
            ) : (
              <form
                className="bg-surface-container-low rounded-xl p-space-lg shadow-sm flex flex-col gap-space-md"
                onSubmit={handleSubmit}
              >
                <div className="rounded-lg border border-outline-variant/30 bg-surface-container-lowest px-space-md py-space-sm flex items-center justify-between gap-space-md">
                  <span className="font-label-md text-label-md text-on-surface">Licence fee</span>
                  <span className="font-headline-sm text-headline-sm text-primary font-bold">
                    {checkoutStatus?.priceLabel ?? SELF_SERVE_PRICE_LABEL}
                  </span>
                </div>
                {checkoutStatus && checkoutStatus.ready === false ? (
                  <div className="flex items-start gap-2 rounded-lg bg-surface-container text-on-surface-variant px-3 py-2 font-body-sm text-body-sm border border-outline-variant/40">
                    <Icon name="info" className="text-[18px] shrink-0" />
                    <span>
                      {checkoutStatus.message ??
                        'Licence checkout is not ready. Start PartFinder-Backend and configure Razorpay in PartFinder-Backend/.env.'}
                    </span>
                  </div>
                ) : null}
                {checkoutStatus?.ready && checkoutStatus.testMode ? (
                  <p className="font-caption text-caption text-outline m-0">
                    Razorpay Test Mode — use test cards from the Razorpay docs; no real money is charged.
                  </p>
                ) : null}
                <div>
                  <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="licence-org">
                    Organisation name
                  </label>
                  <input
                    id="licence-org"
                    className={fieldClass}
                    type="text"
                    autoComplete="organization"
                    placeholder="Northwind Marine"
                    value={organisation}
                    onChange={(event) => setOrganisation(event.target.value)}
                    autoFocus
                    disabled={pending}
                  />
                </div>
                <div>
                  <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="licence-email">
                    Email
                  </label>
                  <input
                    id="licence-email"
                    className={fieldClass}
                    type="email"
                    autoComplete="email"
                    placeholder="ops@company.com"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    disabled={pending}
                  />
                </div>
                <div>
                  <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="licence-mobile">
                    Mobile number
                  </label>
                  <input
                    id="licence-mobile"
                    className={fieldClass}
                    type="tel"
                    autoComplete="tel"
                    inputMode="tel"
                    placeholder="+91 98765 43210"
                    value={mobile}
                    onChange={(event) => setMobile(event.target.value)}
                    disabled={pending}
                  />
                </div>
                {error ? (
                  <div className="flex items-start gap-2 rounded-lg bg-error-container text-on-error-container px-3 py-2 font-body-sm text-body-sm">
                    <Icon name="error" className="text-[18px]" />
                    <span>{error}</span>
                  </div>
                ) : null}
                <button
                  type="submit"
                  className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors flex items-center justify-center gap-1.5 shadow-sm border-0 disabled:opacity-60"
                  disabled={pending}
                >
                  <span>{pending ? 'Opening Razorpay…' : `Proceed to pay ${SELF_SERVE_PRICE_LABEL}`}</span>
                  <Icon name="payments" className="text-[18px]" />
                </button>
                <Link to="/" className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface text-center">
                  Back to home
                </Link>
              </form>
            )}
          </div>
        </section>
      </main>
    </MarketingFrame>
  )
}

function CredentialRow({
  label,
  value,
  mono = false,
}: {
  label: string
  value: string
  mono?: boolean
}) {
  return (
    <div>
      <p className="font-caption text-caption text-outline uppercase tracking-wide m-0 mb-1">{label}</p>
      <p
        className={
          mono
            ? 'font-mono-data text-mono-data text-on-surface m-0 break-all'
            : 'font-body-md text-body-md text-on-surface m-0 break-all'
        }
      >
        {value}
      </p>
    </div>
  )
}
