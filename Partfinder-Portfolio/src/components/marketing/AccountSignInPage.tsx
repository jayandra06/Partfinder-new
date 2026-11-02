import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import {
  type AccountSession,
  readAccountSession,
  readApiMessage,
  saveAccountSession,
  setupApiBase,
} from './accountSession'
import { Icon, MarketingFrame } from './SiteChrome'

const fieldClass =
  'w-full h-11 px-3 bg-surface-container-lowest rounded-lg font-body-sm text-body-sm text-on-surface placeholder:text-outline focus:outline-none focus:ring-2 focus:ring-primary border-0'

export function AccountSignInPage() {
  const navigate = useNavigate()
  const existing = readAccountSession()
  const [orgCode, setOrgCode] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [pending, setPending] = useState(false)
  const [mustChange, setMustChange] = useState<AccountSession | null>(null)
  const [nextPassword, setNextPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')

  if (existing && !mustChange) {
    return <Navigate to="/" replace />
  }

  const finish = (session: AccountSession) => {
    saveAccountSession(session)
    navigate('/', { replace: true })
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const code = orgCode.trim()
    const mail = email.trim().toLowerCase()
    if (!/^\d{6}$/.test(code)) {
      setError('Organisation code must be 6 digits.')
      return
    }
    if (!mail.includes('@')) {
      setError('Enter the email for this PartFinder account.')
      return
    }
    if (!password) {
      setError('Enter the password.')
      return
    }

    setError('')
    setPending(true)
    try {
      const response = await fetch(`${setupApiBase()}/api/public/setup/invite-login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          orgCode: code,
          email: mail,
          temporaryPassword: password,
        }),
      })
      const data: unknown = await response.json().catch(() => null)
      const result = data as {
        ok?: boolean
        message?: string
        email?: string
        role?: string
        accessToken?: string
        mustChangePassword?: boolean
      } | null
      if (!response.ok || !result?.ok || !result.accessToken) {
        setError(readApiMessage(data, result?.message || 'Sign in failed.'))
        return
      }
      const session: AccountSession = {
        email: result.email || mail,
        role: result.role || 'Admin',
        orgCode: code,
        accessToken: result.accessToken,
      }
      if (result.mustChangePassword) {
        setMustChange(session)
        return
      }
      finish(session)
    } catch {
      setError('Cannot reach the server. Is the API running?')
    } finally {
      setPending(false)
    }
  }

  const handleChangePassword = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!mustChange) return
    if (nextPassword.length < 8) {
      setError('New password must be at least 8 characters.')
      return
    }
    if (nextPassword !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }
    setError('')
    setPending(true)
    try {
      const response = await fetch(`${setupApiBase()}/api/public/setup/change-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          orgCode: mustChange.orgCode,
          email: mustChange.email,
          currentPassword: password,
          newPassword: nextPassword,
        }),
      })
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setError(readApiMessage(data, 'Could not update the password.'))
        return
      }
      finish(mustChange)
    } catch {
      setError('Cannot reach the server. Is the API running?')
    } finally {
      setPending(false)
    }
  }

  return (
    <MarketingFrame>
      <main className="w-full pt-20 bg-background">
        <div className="relative w-full overflow-hidden">
          <div className="absolute -top-32 left-1/2 -translate-x-1/2 w-[1100px] h-[480px] bg-gradient-to-b from-primary/10 via-secondary-container/10 to-transparent blur-3xl pointer-events-none -z-10" />
          <section className="max-w-[1360px] mx-auto px-gutter pt-space-lg pb-space-xl">
            <div className="flex items-center gap-space-xs text-primary font-mono-data text-mono-data tracking-widest uppercase bg-surface-container px-space-sm py-1 rounded w-fit mb-space-md">
              <span className="inline-block w-2 h-2 rounded-full bg-primary animate-pulse" />
              <span>PARTFINDER ACCOUNT</span>
            </div>
            <div className="bg-surface-container-lowest rounded-xl p-space-lg md:p-space-xl shadow-lg max-w-2xl">
              <h1 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight m-0">
                {mustChange ? 'Set your password' : 'Sign in'}
              </h1>
              <p className="font-body-md text-body-md text-on-surface-variant mt-space-sm mb-space-lg m-0">
                {mustChange
                  ? 'Choose a password for the same account you use in the PartFinder Windows app.'
                  : 'Sign in with the organisation account you use in the PartFinder Windows app.'}
              </p>

              {mustChange ? (
                <form className="bg-surface-container-low rounded-xl p-space-lg shadow-sm flex flex-col gap-space-md" onSubmit={handleChangePassword}>
                  <div>
                    <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="account-new-password">
                      New password
                    </label>
                    <input
                      id="account-new-password"
                      className={fieldClass}
                      type="password"
                      autoComplete="new-password"
                      value={nextPassword}
                      onChange={(event) => setNextPassword(event.target.value)}
                      autoFocus
                    />
                  </div>
                  <div>
                    <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="account-confirm-password">
                      Confirm password
                    </label>
                    <input
                      id="account-confirm-password"
                      className={fieldClass}
                      type="password"
                      autoComplete="new-password"
                      value={confirmPassword}
                      onChange={(event) => setConfirmPassword(event.target.value)}
                    />
                  </div>
                  {error ? <ErrorNote message={error} /> : null}
                  <button
                    type="submit"
                    className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors border-0 disabled:opacity-60"
                    disabled={pending}
                  >
                    {pending ? 'Saving…' : 'Save and continue'}
                  </button>
                </form>
              ) : (
                <form className="bg-surface-container-low rounded-xl p-space-lg shadow-sm flex flex-col gap-space-md" onSubmit={handleSubmit}>
                  <div>
                    <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="account-org-code">
                      Organisation code
                    </label>
                    <input
                      id="account-org-code"
                      className={fieldClass}
                      inputMode="numeric"
                      autoComplete="off"
                      maxLength={6}
                      placeholder="6-digit code"
                      value={orgCode}
                      onChange={(event) => setOrgCode(event.target.value.replace(/\D/g, '').slice(0, 6))}
                      autoFocus
                    />
                  </div>
                  <div>
                    <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="account-email">
                      Email
                    </label>
                    <input
                      id="account-email"
                      className={fieldClass}
                      type="email"
                      autoComplete="username"
                      placeholder="admin@company.com"
                      value={email}
                      onChange={(event) => setEmail(event.target.value)}
                    />
                  </div>
                  <div>
                    <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="account-password">
                      Password
                    </label>
                    <input
                      id="account-password"
                      className={fieldClass}
                      type="password"
                      autoComplete="current-password"
                      placeholder="Account password"
                      value={password}
                      onChange={(event) => setPassword(event.target.value)}
                    />
                  </div>
                  {error ? <ErrorNote message={error} /> : null}
                  <button
                    type="submit"
                    className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors flex items-center justify-center gap-1.5 shadow-sm border-0 disabled:opacity-60"
                    disabled={pending}
                  >
                    <span>{pending ? 'Signing in…' : 'Sign in'}</span>
                    <Icon name="arrow_forward" className="text-[18px]" />
                  </button>
                  <Link to="/get-licence" className="font-label-md text-label-md text-primary font-bold text-center">
                    Need a licence? Get Licence
                  </Link>
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

function ErrorNote({ message }: { message: string }) {
  return (
    <div className="flex items-start gap-2 rounded-lg bg-error-container text-on-error-container px-3 py-2 font-body-sm text-body-sm">
      <Icon name="error" className="text-[18px]" />
      <span>{message}</span>
    </div>
  )
}
