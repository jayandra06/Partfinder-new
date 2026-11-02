import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { Icon, MarketingFrame } from './SiteChrome'

const fieldClass =
  'w-full h-11 px-3 bg-surface-container-lowest rounded-lg font-body-sm text-body-sm text-on-surface placeholder:text-outline focus:outline-none focus:ring-2 focus:ring-primary border-0'

export function GetLicencePage() {
  const [organisation, setOrganisation] = useState('')
  const [email, setEmail] = useState('')
  const [mobile, setMobile] = useState('')
  const [error, setError] = useState('')
  const [submitted, setSubmitted] = useState<{
    organisation: string
    email: string
    mobile: string
  } | null>(null)

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
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
    setSubmitted({ organisation: name, email: mail, mobile: phone })
  }

  return (
    <MarketingFrame>
      <main className="w-full pt-20 bg-background">
        <div className="relative w-full overflow-hidden">
          <div className="absolute -top-32 left-1/2 -translate-x-1/2 w-[1100px] h-[480px] bg-gradient-to-b from-primary/10 via-secondary-container/10 to-transparent blur-3xl pointer-events-none -z-10" />
          <section className="max-w-[1360px] mx-auto px-gutter pt-space-lg pb-space-xl">
            <div className="flex items-center gap-space-xs text-primary font-mono-data text-mono-data tracking-widest uppercase bg-surface-container px-space-sm py-1 rounded w-fit mb-space-md">
              <span className="inline-block w-2 h-2 rounded-full bg-primary animate-pulse" />
              <span>LICENSING</span>
            </div>
            <div className="bg-surface-container-lowest rounded-xl p-space-lg md:p-space-xl shadow-lg max-w-2xl">
              <h1 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight m-0">
                Get Licence
              </h1>
              <p className="font-body-md text-body-md text-on-surface-variant mt-space-sm mb-space-lg m-0">
                Request a PartFinder licence for your organisation. We use these details to provision your account.
              </p>

              {submitted ? (
                <div className="flex flex-col gap-space-md">
                  <p className="font-body-md text-body-md text-on-surface-variant m-0">
                    Licence request received for{' '}
                    <strong className="text-on-surface">{submitted.organisation}</strong>.
                  </p>
                  <div className="bg-surface-container-low rounded-lg p-space-md flex flex-col gap-2">
                    <p className="font-body-sm text-body-sm text-on-surface m-0">
                      <span className="text-outline">Email</span> · {submitted.email}
                    </p>
                    <p className="font-body-sm text-body-sm text-on-surface m-0">
                      <span className="text-outline">Mobile</span> · {submitted.mobile}
                    </p>
                  </div>
                  <Link
                    to="/"
                    className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors inline-flex items-center justify-center gap-1.5 shadow-sm"
                  >
                    Back to home
                    <Icon name="arrow_forward" className="text-[18px]" />
                  </Link>
                </div>
              ) : (
                <form className="bg-surface-container-low rounded-xl p-space-lg shadow-sm flex flex-col gap-space-md" onSubmit={handleSubmit}>
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
                    className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors flex items-center justify-center gap-1.5 shadow-sm border-0"
                  >
                    <span>Submit licence request</span>
                    <Icon name="arrow_forward" className="text-[18px]" />
                  </button>
                  <Link to="/" className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface text-center">
                    Back to home
                  </Link>
                </form>
              )}
            </div>
          </section>
        </div>
      </main>
    </MarketingFrame>
  )
}
