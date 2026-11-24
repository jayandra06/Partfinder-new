import type { FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { AdminFrame, Icon } from './SiteChrome'

const fieldClass =
  'w-full h-11 px-3 bg-surface-container-lowest rounded-lg font-body-sm text-body-sm text-on-surface placeholder:text-outline focus:outline-none focus:ring-2 focus:ring-primary border-0'

type AdminRegisterViewProps = {
  isGateUnlocked: boolean
  gateInput: string
  showGatePassword: boolean
  gateError: string
  isUnlocking: boolean
  onGateInput: (value: string) => void
  onToggleGatePassword: () => void
  onGateSubmit: (event: FormEvent) => void
  onLockGate: () => void
  fullName: string
  email: string
  role: string
  password: string
  confirmPassword: string
  showPassword: boolean
  onFullName: (value: string) => void
  onEmail: (value: string) => void
  onRole: (value: string) => void
  onPassword: (value: string) => void
  onConfirmPassword: (value: string) => void
  onTogglePassword: () => void
  isSubmitting: boolean
  regError: string
  regSuccess: { name: string; email: string } | null
  onRegisterSubmit: (event: FormEvent) => void
  onEnterDashboard: () => void
}

function Requirement({ met, label }: { met: boolean; label: string }) {
  return (
    <span
      className={`font-mono-data text-caption px-2.5 py-1 rounded-full font-semibold ${
        met ? 'bg-secondary/10 text-secondary' : 'bg-surface-container text-outline'
      }`}
    >
      {met ? '✓' : '•'} {label}
    </span>
  )
}

export function AdminRegisterView(props: AdminRegisterViewProps) {
  const hasMinLength = props.password.length >= 8
  const hasUpper = /[A-Z]/.test(props.password)
  const hasLower = /[a-z]/.test(props.password)
  const hasNumbers = /[0-9]/.test(props.password)
  const isMatch = props.confirmPassword.length > 0 && props.password === props.confirmPassword

  return (
    <AdminFrame>
      <main className="w-full pt-space-xl bg-background">
        <div className="relative w-full overflow-hidden">
          <div className="absolute -top-32 left-1/2 -translate-x-1/2 w-[1100px] h-[480px] bg-gradient-to-b from-primary/10 via-secondary-container/10 to-transparent blur-3xl pointer-events-none -z-10" />
          <section className="max-w-[1360px] mx-auto px-gutter pt-space-lg pb-space-xl">
            <div className="flex items-center gap-space-xs text-primary font-mono-data text-mono-data tracking-widest uppercase bg-surface-container px-space-sm py-1 rounded w-fit mb-space-md">
              <span className="inline-block w-2 h-2 rounded-full bg-primary animate-pulse" />
              <span>ADMIN — REGISTRATION</span>
            </div>

            {!props.isGateUnlocked ? (
              <div className="bg-surface-container-lowest rounded-xl p-space-lg md:p-space-xl shadow-lg">
                <div className="grid grid-cols-1 lg:grid-cols-12 gap-gutter-lg items-center">
                  <div className="lg:col-span-6 flex flex-col gap-space-sm">
                    <span className="inline-flex items-center gap-1.5 w-fit bg-secondary/10 text-secondary px-space-md py-1 rounded-full text-label-sm font-label-sm uppercase tracking-wider">
                      <Icon name="lock" className="text-[15px]" fill />
                      Restricted Admin Access
                    </span>
                    <h1 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight m-0">
                      Security Checkpoint
                    </h1>
                    <p className="font-body-md text-body-md text-on-surface-variant m-0">
                      Enter the master security password to unlock administrator registration for PartFinder.
                    </p>
                    <div className="flex items-center gap-2 mt-space-sm">
                      <Icon name="shield_lock" className="text-primary text-[22px]" />
                      <span className="font-body-sm text-body-sm text-on-surface font-semibold">
                        Registration stays closed until this gate is unlocked.
                      </span>
                    </div>
                  </div>
                  <form
                    className="lg:col-span-6 bg-surface-container-low rounded-xl p-space-lg shadow-sm flex flex-col gap-space-md"
                    onSubmit={props.onGateSubmit}
                  >
                    <div className="flex items-center justify-between pb-space-xs">
                      <span className="font-label-md text-label-md font-bold text-on-surface">
                        Master security password
                      </span>
                      <span className="font-mono-data text-mono-data text-primary font-bold">Gate</span>
                    </div>
                    <div>
                      <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="gate-password">
                        Master Security Password
                      </label>
                      <div className="relative">
                        <input
                          id="gate-password"
                          className={`${fieldClass} pr-12`}
                          type={props.showGatePassword ? 'text' : 'password'}
                          placeholder="Enter security password"
                          value={props.gateInput}
                          onChange={(event) => props.onGateInput(event.target.value)}
                          autoFocus
                          required
                        />
                        <button
                          type="button"
                          className="absolute right-2 top-1/2 -translate-y-1/2 border-0 bg-transparent text-outline hover:text-on-surface p-1"
                          onClick={props.onToggleGatePassword}
                          title={props.showGatePassword ? 'Hide password' : 'Show password'}
                        >
                          <Icon name={props.showGatePassword ? 'visibility_off' : 'visibility'} className="text-[18px]" />
                        </button>
                      </div>
                    </div>
                    {props.gateError ? (
                      <div className="flex items-start gap-2 rounded-lg bg-error-container text-on-error-container px-3 py-2 font-body-sm text-body-sm">
                        <Icon name="error" className="text-[18px]" />
                        <span>{props.gateError}</span>
                      </div>
                    ) : null}
                    <button
                      type="submit"
                      className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors flex items-center justify-center gap-1.5 shadow-sm border-0 disabled:opacity-60"
                      disabled={props.isUnlocking}
                    >
                      <span>{props.isUnlocking ? 'Verifying...' : 'Unlock Registration'}</span>
                      <Icon name="arrow_forward" className="text-[18px]" />
                    </button>
                    <Link to="/admin/login" className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface">
                      Back to admin sign-in
                    </Link>
                  </form>
                </div>
              </div>
            ) : props.regSuccess ? (
              <div className="max-w-3xl mx-auto bg-surface-container-lowest rounded-xl p-space-lg md:p-space-xl shadow-lg text-center">
                <div className="w-16 h-16 mx-auto rounded-full bg-secondary/10 text-secondary flex items-center justify-center mb-space-md">
                  <Icon name="verified" className="text-[36px]" fill />
                </div>
                <span className="inline-flex bg-secondary/10 text-secondary px-space-md py-1 rounded-full text-label-sm font-label-sm uppercase tracking-wider">
                  Admin Activated
                </span>
                <h1 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight mt-space-sm mb-space-sm">
                  Welcome, {props.regSuccess.name}!
                </h1>
                <p className="font-body-md text-body-md text-on-surface-variant m-0">
                  Your administrator account <strong>({props.regSuccess.email})</strong> has been registered with full access.
                </p>
                <div className="mt-space-md inline-flex items-center gap-2 bg-surface-container text-primary font-label-md text-label-md px-space-md py-space-sm rounded-lg">
                  <Icon name="progress_activity" className="text-[18px]" />
                  <span>Redirecting to PartFinder Admin Dashboard...</span>
                </div>
                <div className="mt-space-md">
                  <button
                    type="button"
                    className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors inline-flex items-center justify-center gap-1.5 shadow-sm border-0"
                    onClick={props.onEnterDashboard}
                  >
                    Enter Admin Dashboard Now
                    <Icon name="arrow_forward" className="text-[18px]" />
                  </button>
                </div>
              </div>
            ) : (
              <div className="bg-surface-container-lowest rounded-xl p-space-lg md:p-space-xl shadow-lg">
                <div className="flex flex-col md:flex-row md:items-start justify-between gap-space-md mb-space-lg">
                  <div>
                    <span className="inline-flex items-center gap-1.5 bg-secondary/10 text-secondary px-space-md py-1 rounded-full text-label-sm font-label-sm uppercase tracking-wider">
                      <Icon name="lock_open" className="text-[15px]" />
                      Master Gate Unlocked
                    </span>
                    <h1 className="font-headline-xl text-headline-xl text-on-surface font-extrabold tracking-tight mt-space-sm mb-space-xs">
                      Admin Registration
                    </h1>
                    <p className="font-body-md text-body-md text-on-surface-variant m-0 max-w-2xl">
                      Enter your credentials to register yourself as an administrator to the PartFinder portfolio.
                    </p>
                  </div>
                  <button
                    type="button"
                    className="bg-surface-container text-primary font-label-md text-label-md px-space-md py-space-sm rounded-lg hover:bg-surface-container-high transition-colors border-0 inline-flex items-center gap-1.5 whitespace-nowrap"
                    onClick={props.onLockGate}
                    title="Lock registration gate"
                  >
                    <Icon name="lock" className="text-[16px]" />
                    Lock Gate
                  </button>
                </div>
                <form
                  className="bg-surface-container-low rounded-xl p-space-lg shadow-sm grid grid-cols-1 md:grid-cols-2 gap-space-md"
                  onSubmit={props.onRegisterSubmit}
                >
                  <div>
                    <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="reg-fullname">
                      Full Name
                    </label>
                    <input
                      id="reg-fullname"
                      className={fieldClass}
                      type="text"
                      placeholder="e.g. Jayandra Kumar"
                      value={props.fullName}
                      onChange={(event) => props.onFullName(event.target.value)}
                      required
                    />
                  </div>
                  <div>
                    <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="reg-email">
                      Admin Email ID
                    </label>
                    <input
                      id="reg-email"
                      className={fieldClass}
                      type="email"
                      placeholder="admin@partfinder.com"
                      value={props.email}
                      onChange={(event) => props.onEmail(event.target.value)}
                      required
                    />
                  </div>
                  <div className="md:col-span-2">
                    <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="reg-role">
                      Administrative Role
                    </label>
                    <select
                      id="reg-role"
                      className={fieldClass}
                      value={props.role}
                      onChange={(event) => props.onRole(event.target.value)}
                    >
                      <option value="Super Administrator">Super Administrator (Full System Control)</option>
                      <option value="Portfolio Administrator">Portfolio Administrator (Content & DBs)</option>
                      <option value="Technical Operations">Technical Operations (Deployments & Logs)</option>
                    </select>
                  </div>
                  <div>
                    <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="reg-password">
                      Password
                    </label>
                    <div className="relative">
                      <input
                        id="reg-password"
                        className={`${fieldClass} pr-12`}
                        type={props.showPassword ? 'text' : 'password'}
                        placeholder="Min. 8 characters"
                        value={props.password}
                        onChange={(event) => props.onPassword(event.target.value)}
                        required
                      />
                      <button
                        type="button"
                        className="absolute right-2 top-1/2 -translate-y-1/2 border-0 bg-transparent text-outline hover:text-on-surface p-1"
                        onClick={props.onTogglePassword}
                        title={props.showPassword ? 'Hide password' : 'Show password'}
                      >
                        <Icon name={props.showPassword ? 'visibility_off' : 'visibility'} className="text-[18px]" />
                      </button>
                    </div>
                    {props.password.length > 0 ? (
                      <div className="flex flex-wrap gap-1.5 mt-2">
                        <Requirement met={hasMinLength} label="8+ chars" />
                        <Requirement met={hasUpper} label="Uppercase" />
                        <Requirement met={hasLower} label="Lowercase" />
                        <Requirement met={hasNumbers} label="Number" />
                      </div>
                    ) : null}
                  </div>
                  <div>
                    <label className="font-label-sm text-label-sm text-on-surface block mb-1.5" htmlFor="reg-confirm-password">
                      Confirm Password
                    </label>
                    <input
                      id="reg-confirm-password"
                      className={fieldClass}
                      type={props.showPassword ? 'text' : 'password'}
                      placeholder="Re-enter password"
                      value={props.confirmPassword}
                      onChange={(event) => props.onConfirmPassword(event.target.value)}
                      required
                    />
                    {props.confirmPassword.length > 0 ? (
                      <span className={`font-caption text-caption block mt-2 font-semibold ${isMatch ? 'text-secondary' : 'text-error'}`}>
                        {isMatch ? '✓ Passwords match' : '✕ Passwords do not match'}
                      </span>
                    ) : null}
                  </div>
                  {props.regError ? (
                    <div className="md:col-span-2 flex items-start gap-2 rounded-lg bg-error-container text-on-error-container px-3 py-2 font-body-sm text-body-sm">
                      <Icon name="error" className="text-[18px]" />
                      <span>{props.regError}</span>
                    </div>
                  ) : null}
                  <div className="md:col-span-2 flex flex-col gap-space-md">
                    <button
                      type="submit"
                      className="bg-primary hover:bg-primary-container text-on-primary font-label-md text-label-md px-space-md py-2.5 rounded-lg transition-colors flex items-center justify-center gap-1.5 shadow-sm border-0 disabled:opacity-60"
                      disabled={props.isSubmitting}
                    >
                      <Icon name="person_add" className="text-[18px]" />
                      <span>{props.isSubmitting ? 'Registering Admin Account...' : 'Register as Admin'}</span>
                    </button>
                    <Link to="/admin/login" className="font-label-md text-label-md text-on-surface-variant hover:text-on-surface">
                      Already have an account? Sign in
                    </Link>
                  </div>
                </form>
              </div>
            )}
          </section>
        </div>
      </main>
    </AdminFrame>
  )
}
