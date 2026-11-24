/** Base URL for PartFinder-Backend (no trailing slash, no `/api` suffix). */
export function normalizeApiBase(rawUrl?: string): string {
  if (rawUrl && typeof rawUrl === 'string' && rawUrl.trim() !== '') {
    let trimmed = rawUrl.trim().replace(/\/$/, '')
    if (!/^https?:\/\//i.test(trimmed)) {
      trimmed = `https://${trimmed}`
    }
    if (trimmed.toLowerCase().endsWith('/api')) {
      trimmed = trimmed.slice(0, -4)
    }
    return trimmed
  }
  return import.meta.env.DEV ? '' : 'https://backend.partfinder.store'
}

export const apiBase = normalizeApiBase(import.meta.env.VITE_API_URL)
