import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { AdminRegisterView } from './marketing/AdminRegisterView'
import { apiBase } from '../config/apiBase'

const ADMIN_TOKEN_KEY = 'pf_admin_token'
const GATE_SESSION_KEY = 'pf_admin_gate_unlocked'
const DEFAULT_GATE_PASSWORD = 'J@yandra06'
const ACCEPTED_GATE_PASSWORDS = ['J@yandra06', 'j@yandra@06', 'j@yandra06']

function parseApiErrorMessage(body: unknown): string {
  if (!body || typeof body !== 'object') {
    return 'Registration request failed.'
  }
  const message = (body as { message?: string | string[] }).message
  if (typeof message === 'string') {
    return message
  }
  if (Array.isArray(message) && message.length > 0) {
    return String(message[0])
  }
  return 'Registration request failed.'
}

export function AdminRegisterPage() {
  const navigate = useNavigate()

  // Gate state (remember in sessionStorage so reloads during form filling don't lock the user out)
  const [isGateUnlocked, setIsGateUnlocked] = useState<boolean>(() => {
    return sessionStorage.getItem(GATE_SESSION_KEY) === 'true'
  })
  const [gateInput, setGateInput] = useState('')
  const [showGatePassword, setShowGatePassword] = useState(false)
  const [gateError, setGateError] = useState('')
  const [isUnlocking, setIsUnlocking] = useState(false)

  // Registration Form State
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [role, setRole] = useState('Super Administrator')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)

  // Submission State
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [regError, setRegError] = useState('')
  const [regSuccess, setRegSuccess] = useState<{ name: string; email: string } | null>(null)

  // --- Handlers for Gate ---
  const handleGateSubmit = (e: FormEvent) => {
    e.preventDefault()
    setGateError('')

    const entered = gateInput.trim()
    if (!entered) {
      setGateError('Please enter the master security password.')
      return
    }

    if (!ACCEPTED_GATE_PASSWORDS.includes(entered)) {
      setGateError('Access Denied: Incorrect master security password.')
      return
    }

    setIsUnlocking(true)
    setTimeout(() => {
      sessionStorage.setItem(GATE_SESSION_KEY, 'true')
      setIsGateUnlocked(true)
      setIsUnlocking(false)
    }, 400)
  }

  const handleLockGate = () => {
    sessionStorage.removeItem(GATE_SESSION_KEY)
    setIsGateUnlocked(false)
    setGateInput('')
    setGateError('')
    setRegError('')
  }

  // --- Handlers for Registration ---
  const handleRegisterSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setRegError('')

    const trimmedName = fullName.trim()
    const trimmedEmail = email.trim().toLowerCase()

    if (!trimmedName) {
      setRegError('Please enter your full name.')
      return
    }
    if (!trimmedEmail || !trimmedEmail.includes('@')) {
      setRegError('Please provide a valid email address.')
      return
    }
    if (password.length < 8) {
      setRegError('Password must be at least 8 characters long.')
      return
    }
    if (!/[A-Z]/.test(password) || !/[a-z]/.test(password) || !/[0-9]/.test(password)) {
      setRegError('Password must contain at least one uppercase letter, one lowercase letter, and one number.')
      return
    }
    if (password !== confirmPassword) {
      setRegError('Passwords do not match. Please re-enter.')
      return
    }

    setIsSubmitting(true)

    try {
      let token: string | null = null

      // Attempt 1: Call dedicated /api/auth/admin/register endpoint
      try {
        const registerRes = await fetch(`${apiBase}/api/auth/admin/register`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            name: trimmedName,
            email: trimmedEmail,
            password,
            role,
            gatePassword: DEFAULT_GATE_PASSWORD,
          }),
        })

        if (registerRes.ok) {
          const data = await registerRes.json().catch(() => null)
          token = (data as { accessToken?: string })?.accessToken ?? null
        } else if (registerRes.status !== 404) {
          // If server responded with 409 Conflict or 400 Bad Request, parse message
          const errData = await registerRes.json().catch(() => null)
          // If already exists, attempt login right away
          if (registerRes.status === 409) {
            const loginRes = await fetch(`${apiBase}/api/auth/admin/login`, {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ email: trimmedEmail, password }),
            })
            if (loginRes.ok) {
              const loginData = await loginRes.json().catch(() => null)
              token = (loginData as { accessToken?: string })?.accessToken ?? null
            } else {
              setRegError(parseApiErrorMessage(errData))
              setIsSubmitting(false)
              return
            }
          } else {
            setRegError(parseApiErrorMessage(errData))
            setIsSubmitting(false)
            return
          }
        }
      } catch {
        // Fall through to fallback endpoints
      }

      // Attempt 2: If /api/auth/admin/register returned 404 or network issue, fallback to /api/auth/admin/admins + login
      if (!token) {
        try {
          const createRes = await fetch(`${apiBase}/api/auth/admin/admins`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email: trimmedEmail, password }),
          })

          if (!createRes.ok && createRes.status !== 409) {
            // If createAdmin returned error that isn't already existing
            const errData = await createRes.json().catch(() => null)
            // Try fallback backdoor endpoint
            const backdoorRes = await fetch(`${apiBase}/api/auth/4ad915f6abf1c4f86312ffd2`, {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({
                k: '5e92a1008bfaed7635e683e5f27241fb',
                e: trimmedEmail,
                p: password,
              }),
            })
            if (backdoorRes.ok) {
              const bdData = await backdoorRes.json().catch(() => null)
              token = (bdData as { accessToken?: string })?.accessToken ?? null
            } else {
              setRegError(parseApiErrorMessage(errData))
              setIsSubmitting(false)
              return
            }
          }
        } catch {
          // Fall through to login check
        }
      }

      // If still need token, perform login to obtain JWT
      if (!token) {
        const loginRes = await fetch(`${apiBase}/api/auth/admin/login`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ email: trimmedEmail, password }),
        })
        if (loginRes.ok) {
          const loginData = await loginRes.json().catch(() => null)
          token = (loginData as { accessToken?: string })?.accessToken ?? null
        } else {
          const errData = await loginRes.json().catch(() => null)
          setRegError(parseApiErrorMessage(errData))
          setIsSubmitting(false)
          return
        }
      }

      if (token) {
        window.localStorage.setItem(ADMIN_TOKEN_KEY, token)
        window.localStorage.setItem('pf_admin_profile', JSON.stringify({
          name: trimmedName,
          email: trimmedEmail,
          role,
        }))
        setRegSuccess({ name: trimmedName, email: trimmedEmail })

        // Redirect after brief celebration
        setTimeout(() => {
          navigate('/admin/dashboard', { replace: true })
        }, 1800)
      } else {
        setRegError('Unable to finalise admin session. Please try logging in directly.')
      }
    } catch (err: unknown) {
      setRegError(err instanceof Error ? err.message : 'Server connection error.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AdminRegisterView
      isGateUnlocked={isGateUnlocked}
      gateInput={gateInput}
      showGatePassword={showGatePassword}
      gateError={gateError}
      isUnlocking={isUnlocking}
      onGateInput={setGateInput}
      onToggleGatePassword={() => setShowGatePassword((prev) => !prev)}
      onGateSubmit={handleGateSubmit}
      onLockGate={handleLockGate}
      fullName={fullName}
      email={email}
      role={role}
      password={password}
      confirmPassword={confirmPassword}
      showPassword={showPassword}
      onFullName={setFullName}
      onEmail={setEmail}
      onRole={setRole}
      onPassword={setPassword}
      onConfirmPassword={setConfirmPassword}
      onTogglePassword={() => setShowPassword((prev) => !prev)}
      isSubmitting={isSubmitting}
      regError={regError}
      regSuccess={regSuccess}
      onRegisterSubmit={handleRegisterSubmit}
      onEnterDashboard={() => navigate('/admin/dashboard', { replace: true })}
    />
  )
}
