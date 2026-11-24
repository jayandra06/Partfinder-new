import { useState } from 'react'
import type { FormEvent } from 'react'
import { AdminFrame, Icon } from './SiteChrome'

const fieldClass =
  'w-full h-11 px-3 bg-surface-container-lowest rounded-lg font-body-sm text-body-sm text-on-surface placeholder:text-outline focus:outline-none focus:ring-2 focus:ring-primary border-0'

type AdminLoginViewProps = {
  email: string
  password: string
  error: string
  loginPending: boolean
  onEmailChange: (value: string) => void
  onPasswordChange: (value: string) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}

export function AdminLoginView({
  email,
  password,
  error,
  loginPending,
  onEmailChange,
  onPasswordChange,
  onSubmit,
}: AdminLoginViewProps) {
  const [showPassword, setShowPassword] = useState(false)

  return (
    <AdminFrame>
      <main className="w-full pt-space-xl bg-background">
        <div className="relative w-full overflow-hidden">
          <div className="absolute -top-32 left-1/2 -translate-x-1/2 w-[1100px] h-[480px] bg-gradient-to-b from-primary/10 via-secondary-container/10 to-transparent blur-3xl pointer-events-none -z-10" />
          <section className="max-w-[1360px] mx-auto px-gutter pt-space-lg pb-space-xl">
            <div className="flex items-center gap-space-xs text-primary font-mono-data text-mono-data tracking-widest uppercase bg-surface-container px-space-sm py-1 rounded w-fit mb-space-md">
              <span className="inline-block w-2 h-2 rounded-full bg-primary animate-pulse" />
              <span>ADMIN — SIGN IN</span>
            </div>
            <div className="bg-surface-container-lowest rounded-xl p-space-lg md:p-space-xl shadow-lg">
              <div className="grid grid-cols-1 lg:grid-cols-12 gap-gutter-lg items-center">
                <div className="lg:col-span-6 flex flex-col gap-space-sm">
                  <span className="font-caption text-caption text-primary uppercase font-bold tracking-widest">
                    Operations Portal
                  </span>
                  <h1 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight m-0">
                    Sign in to PartFinder Admin
                  </h1>
                  <p className="font-body-md text-body-md text-on-surface-variant m-0">
                    Use the administrator email and password already provisioned for this portfolio. Registration stays behind the same security gate.
                  </p>
                  <div className="flex flex-col gap-2 mt-space-sm">
                    <div className="flex items-center gap-2">
                      <Icon name="check_circle" className="text-primary text-[20px]" />
                      <span className="font-body-sm text-body-sm text-on-surface font-semibold">
                        Organisations, releases, and database clusters
                      </span>
                    </div>
                    <div className="flex items-center gap-2">
                      <Icon name="check_circle" className="text-primary text-[20px]" />
                      <span className="font-body-sm text-body-sm text-on-surface font-semibold">
                        Session stays on this browser until you log out
                      </span>
                    </div>
                    <div className="flex items-center gap-2">
                      <Icon name="check_circle" className="text-primary text-[20px]" />
                      <span className="font-body-sm text-body-sm text-on-surface font-semibold">
                        New administrators register through the security checkpoint
                      </span>
                    </div>
                  </div>
                </div>
                <form
                  className="lg:col-span-6 bg-surface-container-low rounded-xl p-space-lg shadow-sm flex flex-col gap-space-md"
                  onSubmit={onSubmit}
                >
                  <div className="flex items-center justify-between pb-space-xs">
                    <span className="font-label-md text-label-md font-bold text-on-surface">
                      Administrator credentials
                    </span>
                    <span className="font-mono-data text-mono-data text-primary font-bold">Secure</span>
                  </div>
                  <div>
                    <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="admin-email">
                      Email ID
                    </label>
                    <input
                      id="admin-email"
                      className={fieldClass}
                      type="email"
                      placeholder="admin@partfinder.com"
                      value={email}
                      onChange={(event) => onEmailChange(event.target.value)}
                      autoComplete="username"
                    />
                  </div>
                  <div>
                    <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="admin-password">
                      Password
                    </label>
                    <div className="relative">
                      <input
                        id="admin-password"
                        className={`${fieldClass} pr-11`}
                        type={showPassword ? 'text' : 'password'}
                        placeholder="Enter password"
                        value={password}
                        onChange={(event) => onPasswordChange(event.target.value)}
                        autoComplete="current-password"
                      />
                      <button
                        type="button"
                        className="absolute right-1 top-1/2 -translate-y-1/2 w-9 h-9 flex items-center justify-center rounded-lg text-on-surface-variant hover:text-on-surface hover:bg-surface-container-high/80 border-0 bg-transparent p-0 cursor-pointer"
                        onClick={() => setShowPassword((prev) => !prev)}
                        aria-label={showPassword ? 'Hide password' : 'Show password'}
                        aria-pressed={showPassword}
                      >
                        <Icon
                          name={showPassword ? 'visibility_off' : 'visibility'}
                          className="text-[22px]"
                        />
                      </button>
                    </div>
                  </div>
                  {error ? (
                    <div className="flex items-start gap-2 rounded-lg bg-error-container text-on-error-container px-3 py-2 font-body-sm text-body-sm">
                      <Icon name="error" className="text-[18px] mt-0.5" />
                      <span>{error}</span>
                    </div>
                  ) : null}
                  <button
                    type="submit"
                    className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors flex items-center justify-center gap-1.5 shadow-sm border-0 disabled:opacity-60"
                    disabled={loginPending}
                  >
                    <span>{loginPending ? 'Signing in…' : 'Login'}</span>
                    <Icon name="arrow_forward" className="text-[18px]" />
                  </button>
                  <div className="flex items-center gap-2 font-caption text-caption text-outline">
                    <Icon name="lock" className="text-[16px] text-secondary" />
                    <span>Encrypted sign-in. Credentials are checked by the existing admin API.</span>
                  </div>
                  <p className="font-caption text-caption text-outline m-0 pt-space-xs">
                    Platform operator access only. Registration and sign-in URLs are not listed on the public site.
                  </p>
                </form>
              </div>
            </div>
          </section>
        </div>
      </main>
    </AdminFrame>
  )
}
