export const ACCOUNT_SESSION_KEY = 'pf_account_session'

export type AccountSession = {
  email: string
  role: string
  orgCode: string
  accessToken: string
}

/** Origin for API calls. Empty string uses same-origin `/api` (Vite proxy in dev, Vercel rewrite in prod). */
export function setupApiBase(): string {
  const raw = import.meta.env.VITE_API_URL
  if (typeof raw === 'string' && raw.trim() !== '') {
    let trimmed = raw.trim().replace(/\/$/, '')
    if (!/^https?:\/\//i.test(trimmed)) trimmed = `https://${trimmed}`
    if (trimmed.endsWith('/api')) trimmed = trimmed.slice(0, -4)
    return trimmed
  }
  return ''
}

/** Build a full URL for a Nest route under the global `/api` prefix. */
export function buildApiUrl(path: string): string {
  const segment = path.startsWith('/') ? path : `/${path}`
  const withPrefix = segment.startsWith('/api/') ? segment : `/api${segment}`
  const base = setupApiBase()
  return base ? `${base}${withPrefix}` : withPrefix
}

export function readApiMessage(body: unknown, fallback: string): string {
  if (!body || typeof body !== 'object') return fallback
  const message = (body as { message?: string | string[] }).message
  if (typeof message === 'string' && message.trim()) return message
  if (Array.isArray(message) && message.length > 0) return String(message[0])
  return fallback
}

export function readFetchErrorMessage(
  response: Response,
  body: unknown,
  fallback: string,
): string {
  if (body && typeof body === 'object') {
    const fromJson = readApiMessage(body, '')
    if (fromJson) return fromJson
  }
  if (typeof body === 'string' && body.trim()) {
    const text = body.trim()
    if (/cannot (get|post|put|patch|delete)/i.test(text)) {
      return 'Licence checkout API is not available on this server. Run PartFinder-Backend locally (port 3000) or deploy the latest backend with Razorpay configured.'
    }
    return text
  }
  if (response.status === 404) {
    return 'Licence checkout API was not found (404). Deploy the latest PartFinder-Backend to backend.partfinder.store, or run the API locally on port 3000.'
  }
  if (response.status === 502 || response.status === 503 || response.status === 504) {
    return `Payment API is unreachable (HTTP ${response.status}). Start PartFinder-Backend on port 3000 for local dev, or check the production API deployment.`
  }
  if (response.status >= 400) {
    return `${fallback} (HTTP ${response.status}${response.statusText ? `: ${response.statusText}` : ''})`
  }
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
