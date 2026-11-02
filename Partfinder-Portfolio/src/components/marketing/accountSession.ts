export const ACCOUNT_SESSION_KEY = 'pf_account_session'

export type AccountSession = {
  email: string
  role: string
  orgCode: string
  accessToken: string
}

export function setupApiBase(): string {
  const raw = import.meta.env.VITE_API_URL
  if (typeof raw === 'string' && raw.trim() !== '') {
    let trimmed = raw.trim().replace(/\/$/, '')
    if (!/^https?:\/\//i.test(trimmed)) trimmed = `https://${trimmed}`
    return trimmed
  }
  return import.meta.env.DEV ? '' : 'https://backend.partfinder.store'
}

export function readApiMessage(body: unknown, fallback: string): string {
  if (!body || typeof body !== 'object') return fallback
  const message = (body as { message?: string | string[] }).message
  if (typeof message === 'string' && message.trim()) return message
  if (Array.isArray(message) && message.length > 0) return String(message[0])
  return fallback
}

export function readAccountSession(): AccountSession | null {
  try {
    const raw = window.localStorage.getItem(ACCOUNT_SESSION_KEY)
    if (!raw) return null
    const parsed = JSON.parse(raw) as AccountSession
    if (!parsed?.email || !parsed?.accessToken) return null
    return parsed
  } catch {
    return null
  }
}

export function clearAccountSession() {
  window.localStorage.removeItem(ACCOUNT_SESSION_KEY)
}

export function saveAccountSession(session: AccountSession) {
  window.localStorage.setItem(ACCOUNT_SESSION_KEY, JSON.stringify(session))
}
