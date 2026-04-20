import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, NavLink, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'

const ADMIN_TOKEN_KEY = 'pf_admin_token'

/** In dev, prefer same-origin `/api` (Vite proxy) unless VITE_API_URL is set — avoids stale SW/caches to :3000. */
const apiBase =
  import.meta.env.VITE_API_URL?.replace(/\/$/, '') ??
  (import.meta.env.DEV ? '' : 'http://localhost:3000')

function parseApiErrorMessage(body: unknown): string {
  if (!body || typeof body !== 'object') {
    return 'Request failed'
  }
  const message = (body as { message?: string | string[] }).message
  if (typeof message === 'string') {
    return message
  }
  if (Array.isArray(message) && message.length > 0) {
    return String(message[0])
  }
  return 'Request failed'
}

function readStoredAuth(): boolean {
  if (typeof window === 'undefined') {
    return false
  }
  return Boolean(window.localStorage.getItem(ADMIN_TOKEN_KEY))
}

function authJsonHeaders(): HeadersInit {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  const token =
    typeof window !== 'undefined'
      ? window.localStorage.getItem(ADMIN_TOKEN_KEY)
      : null
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }
  return headers
}

function postLoginPath(pathname: string): string {
  if (pathname === '/admin' || pathname === '/admin/') {
    return '/admin/dashboard'
  }
  if (pathname.startsWith('/admin/')) {
    return pathname
  }
  return '/admin/dashboard'
}

function App() {
  return (
    <Routes>
      <Route path="/" element={<MarketingHomePage />} />
      <Route path="/admin/*" element={<AdminPortalPage />} />
      <Route path="/home" element={<Navigate to="/" replace />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}

function MarketingHomePage() {
  return (
    <main className="page">
      <header className="hero">
        <p className="badge">PartFinder Platform</p>
        <h1>Modern spare parts platform for vendors and manufacturers</h1>
        <p className="sub">
          Manage parts, users, and organizations from one simple admin panel.
        </p>
        <div className="actions">
          <Link to="/admin" className="btn-primary">
            Admin Panel
          </Link>
        </div>
      </header>
    </main>
  )
}

function AdminPortalPage() {
  const defaultDevEmail =
    import.meta.env.MODE === 'development' ? 'jayandraa5@gmail.com' : ''
  const [email, setEmail] = useState(defaultDevEmail)
  const [password, setPassword] = useState('')
  const [isAuthenticated, setIsAuthenticated] = useState(readStoredAuth)
  const [error, setError] = useState('')
  const [loginPending, setLoginPending] = useState(false)
  const navigate = useNavigate()
  const location = useLocation()

  const handleSessionExpired = useCallback(() => {
    window.localStorage.removeItem(ADMIN_TOKEN_KEY)
    setIsAuthenticated(false)
    navigate('/admin', { replace: true })
  }, [navigate])

  useEffect(() => {
    if (!isAuthenticated) {
      return
    }
    if (location.pathname === '/admin' || location.pathname === '/admin/') {
      navigate('/admin/dashboard', { replace: true })
    }
  }, [isAuthenticated, location.pathname, navigate])

  const handleLogin = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    if (!email.trim() || !password.trim()) {
      setError('Please enter email and password.')
      return
    }

    setError('')
    setLoginPending(true)
    try {
      const response = await fetch(`${apiBase}/api/auth/admin/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email: email.trim(), password }),
      })
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setError(parseApiErrorMessage(data))
        return
      }
      const token = (data as { accessToken?: string }).accessToken
      if (!token) {
        setError('Invalid response from server.')
        return
      }
      window.localStorage.setItem(ADMIN_TOKEN_KEY, token)
      setIsAuthenticated(true)
      navigate(postLoginPath(location.pathname), { replace: true })
    } catch {
      setError('Cannot reach the server. Is the API running?')
    } finally {
      setLoginPending(false)
    }
  }

  if (!isAuthenticated) {
    return (
      <main className="auth-page">
        <section className="auth-card">
          <h1>Admin Portal Login</h1>
          <p>Sign in with your email and password.</p>

          <form className="auth-form" onSubmit={handleLogin}>
            <label htmlFor="admin-email">Email ID</label>
            <input
              id="admin-email"
              type="email"
              placeholder="admin@partfinder.com"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />

            <label htmlFor="admin-password">Password</label>
            <input
              id="admin-password"
              type="password"
              placeholder="Enter password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />

            {error ? <p className="form-error">{error}</p> : null}

            <button type="submit" className="btn-primary" disabled={loginPending}>
              {loginPending ? 'Signing in…' : 'Login'}
            </button>
          </form>
        </section>
      </main>
    )
  }

  const isOrganizationsPage = location.pathname.startsWith('/admin/organizations')
  const isUsersPage = location.pathname.startsWith('/admin/users')
  const isSettingsPage = location.pathname.startsWith('/admin/settings')
  const isDebugPage = location.pathname.startsWith('/admin/debug')
  const isReleasesPage = location.pathname.startsWith('/admin/releases')
  const isDatabaseManagementPage = location.pathname.startsWith(
    '/admin/database-management',
  )
  const databaseClusterDetailMatch = location.pathname.match(
    /^\/admin\/database-management\/cluster\/([^/]+)\/?$/,
  )
  const databaseClusterDetailId = databaseClusterDetailMatch?.[1] ?? null
  const pageTitle = isOrganizationsPage
    ? 'Organizations'
    : isUsersPage
      ? 'User Management'
      : isSettingsPage
        ? 'Settings'
        : isDebugPage
          ? 'Debug Logs'
          : isReleasesPage
            ? 'Release Management'
            : isDatabaseManagementPage
              ? databaseClusterDetailId
                ? 'Cluster organizations'
                : 'Database Management'
              : 'Dashboard'

  return (
    <div className="admin-layout">
      <aside className="admin-sidebar">
        <div className="admin-sidebar-head">
          <h2>PartFinder Admin</h2>
          <span className="sidebar-gear">⚙</span>
        </div>
        <nav>
          <NavLink to="/admin/dashboard">Dashboard</NavLink>
          <NavLink to="/admin/organizations">Organizations</NavLink>
          <NavLink to="/admin/database-management">Database Management</NavLink>
          <NavLink to="/admin/users">User Management</NavLink>
          <NavLink to="/admin/releases">Releases</NavLink>
          <button type="button" className="side-item-muted">Analytics</button>
          <NavLink to="/admin/settings">Settings</NavLink>
          <NavLink to="/admin/debug">Debug</NavLink>
        </nav>
        <button
          type="button"
          className="sidebar-logout"
          onClick={() => {
            window.localStorage.removeItem(ADMIN_TOKEN_KEY)
            setIsAuthenticated(false)
            navigate('/admin', { replace: true })
          }}
        >
          Logout
        </button>
      </aside>
      <main className="admin-main">
        <header className="admin-topbar">
          <div>
            <h1>{pageTitle}</h1>
            <p>
              {isOrganizationsPage
                ? 'Manage platform organizations and their settings'
                : isDatabaseManagementPage
                  ? databaseClusterDetailId
                    ? 'Organizations provisioned on this cluster via Default database mode'
                    : 'Register MongoDB clusters for default organization databases'
                  : isSettingsPage
                    ? 'Manage administrator credentials and access'
                    : isDebugPage
                      ? 'Monitor system events and diagnostics'
                      : isReleasesPage
                        ? 'Manage PartFinder releases and installations'
                        : 'Welcome to the admin portal'}
            </p>
          </div>
        </header>
        {isOrganizationsPage ? (
          <OrganizationsSection onSessionExpired={handleSessionExpired} />
        ) : null}
        {isUsersPage ? <UsersSection /> : null}
        {isSettingsPage ? (
          <SettingsSection onSessionExpired={handleSessionExpired} />
        ) : null}
        {isDebugPage ? (
          <DebugSection onSessionExpired={handleSessionExpired} />
        ) : null}
        {isReleasesPage ? <ReleasesSection /> : null}
        {isDatabaseManagementPage ? (
          databaseClusterDetailId ? (
            <DatabaseClusterOrganizationsSection
              clusterId={databaseClusterDetailId}
              onSessionExpired={handleSessionExpired}
            />
          ) : (
            <DatabaseManagementSection onSessionExpired={handleSessionExpired} />
          )
        ) : null}
        {!isOrganizationsPage &&
        !isUsersPage &&
        !isSettingsPage &&
        !isDebugPage &&
        !isReleasesPage &&
        !isDatabaseManagementPage ? (
          <DashboardSection />
        ) : null}
      </main>
    </div>
  )
}

function DashboardSection() {
  return (
    <section className="admin-kpi-grid">
      <article className="card">
        <h3>Total Organizations</h3>
        <p className="kpi-value">42</p>
      </article>
      <article className="card">
        <h3>Active Users</h3>
        <p className="kpi-value">1,268</p>
      </article>
      <article className="card">
        <h3>New Sign-ins Today</h3>
        <p className="kpi-value">87</p>
      </article>
    </section>
  )
}

type ApiOrganization = {
  id: string
  orgCode: string
  name: string
  type: string
  plan: string
  validity: string
  status: string
  licensePermanentlyBanned?: boolean
  licenseBannedUntil?: string | null
  createdAt: string
}

type OrgRow = {
  id: string
  code: string
  name: string
  type: string
  plan: string
  validity: string
  status: string
  licensePermanentlyBanned: boolean
  licenseBannedUntil: string | null
  created: string
}

function mapOrgToRow(o: ApiOrganization): OrgRow {
  const validity = o.validity ? new Date(o.validity).toISOString().slice(0, 10) : '—'
  const created = o.createdAt
    ? new Date(o.createdAt).toLocaleDateString()
    : '—'
  const until =
    o.licenseBannedUntil == null || o.licenseBannedUntil === ''
      ? null
      : o.licenseBannedUntil
  return {
    id: o.id,
    code: o.orgCode,
    name: o.name,
    type: o.type,
    plan: o.plan,
    validity,
    status: o.status,
    licensePermanentlyBanned: Boolean(o.licensePermanentlyBanned),
    licenseBannedUntil: until,
    created,
  }
}

function isLicenseCurrentlyBlocked(row: OrgRow): boolean {
  if (row.licensePermanentlyBanned) {
    return true
  }
  if (!row.licenseBannedUntil) {
    return false
  }
  const t = new Date(row.licenseBannedUntil).getTime()
  return !Number.isNaN(t) && Date.now() < t
}

/** True if reactivate should be offered (active ban or stale temporary end date to clear). */
function showLicenseReactivateOption(row: OrgRow): boolean {
  return row.licensePermanentlyBanned || Boolean(row.licenseBannedUntil)
}

function licensePillClass(row: OrgRow): string {
  if (row.licensePermanentlyBanned) {
    return 'pill pill-license-revoked'
  }
  if (row.licenseBannedUntil) {
    const t = new Date(row.licenseBannedUntil).getTime()
    if (!Number.isNaN(t) && Date.now() < t) {
      return 'pill pill-license-temp'
    }
  }
  return 'pill pill-license-ok'
}

function licensePillLabel(row: OrgRow): string {
  if (row.licensePermanentlyBanned) {
    return 'Revoked'
  }
  if (row.licenseBannedUntil) {
    const t = new Date(row.licenseBannedUntil).getTime()
    if (!Number.isNaN(t) && Date.now() < t) {
      return `Until ${new Date(row.licenseBannedUntil).toLocaleString()}`
    }
  }
  return 'OK'
}

function typePillClass(type: string): string {
  if (type === 'premium') {
    return 'pill pill-premium'
  }
  if (type === 'enterprise') {
    return 'pill pill-enterprise'
  }
  return 'pill pill-standard'
}

function planPillClass(plan: string): string {
  switch (plan) {
    case 'demo':
      return 'pill pill-demo'
    case 'trial':
      return 'pill pill-trial'
    case 'starter':
      return 'pill pill-starter'
    case 'professional':
      return 'pill pill-professional'
    case 'annual':
      return 'pill pill-annual'
    case 'lifetime':
      return 'pill pill-lifetime'
    default:
      return 'pill pill-plan-legacy'
  }
}

function statusPillClass(status: string): string {
  return status.trim().toLowerCase() === 'suspended'
    ? 'pill pill-suspended'
    : 'pill pill-active'
}

function OrganizationsSection({
  onSessionExpired,
}: {
  onSessionExpired: () => void
}) {
  type ModalMode = 'create' | 'edit'
  const [modalMode, setModalMode] = useState<ModalMode>('create')
  const [editingId, setEditingId] = useState<string | null>(null)
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [orgName, setOrgName] = useState('')
  const [orgType, setOrgType] = useState('')
  const [orgPlan, setOrgPlan] = useState('')
  const [orgStatus, setOrgStatus] = useState('Active')
  const [firstAdminEmail, setFirstAdminEmail] = useState('')
  const [generatedCode, setGeneratedCode] = useState('')
  const [search, setSearch] = useState('')
  const [rows, setRows] = useState<OrgRow[]>([])
  const [loadError, setLoadError] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const [modalError, setModalError] = useState('')
  const [banModalRow, setBanModalRow] = useState<OrgRow | null>(null)
  const [banPermanent, setBanPermanent] = useState(false)
  const [banMinutes, setBanMinutes] = useState('')
  const [banHours, setBanHours] = useState('')
  const [banDays, setBanDays] = useState('')
  const [banSaving, setBanSaving] = useState(false)
  const [banModalError, setBanModalError] = useState('')
  const [reactivatingId, setReactivatingId] = useState<string | null>(null)

  const loadOrganizations = useCallback(async () => {
    setLoadError('')
    setLoading(true)
    try {
      const response = await fetch(`${apiBase}/api/organizations`, {
        headers: authJsonHeaders(),
      })
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setLoadError(parseApiErrorMessage(data))
        return
      }
      const list = Array.isArray(data) ? (data as ApiOrganization[]) : []
      setRows(list.map(mapOrgToRow))
    } catch {
      setLoadError('Cannot load organizations. Is the API running?')
    } finally {
      setLoading(false)
    }
  }, [onSessionExpired])

  useEffect(() => {
    void loadOrganizations()
  }, [loadOrganizations])

  const filteredRows = useMemo(() => {
    const q = search.trim().toLowerCase()
    if (!q) {
      return rows
    }
    return rows.filter(
      (r) =>
        r.code.toLowerCase().includes(q) ||
        r.name.toLowerCase().includes(q) ||
        r.plan.toLowerCase().includes(q),
    )
  }, [rows, search])

  const canSave = useMemo(() => {
    if (modalMode === 'create') {
      return Boolean(
        orgName.trim() &&
          orgType &&
          orgPlan &&
          generatedCode &&
          firstAdminEmail.trim() &&
          firstAdminEmail.includes('@'),
      )
    }
    return Boolean(editingId && orgName.trim() && orgType && orgPlan && orgStatus)
  }, [
    modalMode,
    editingId,
    orgName,
    orgType,
    orgPlan,
    orgStatus,
    generatedCode,
    firstAdminEmail,
  ])

  const generateCode = () => {
    const code = Math.floor(100000 + Math.random() * 900000).toString()
    setGeneratedCode(code)
  }

  const openCreateModal = () => {
    setModalMode('create')
    setEditingId(null)
    setOrgName('')
    setOrgType('')
    setOrgPlan('')
    setOrgStatus('Active')
    setFirstAdminEmail('')
    setGeneratedCode('')
    setModalError('')
    setIsModalOpen(true)
  }

  const openEditModal = (row: OrgRow) => {
    setModalMode('edit')
    setEditingId(row.id)
    setOrgName(row.name)
    setOrgType(row.type)
    setOrgPlan(row.plan)
    setOrgStatus(row.status)
    setGeneratedCode(row.code)
    setModalError('')
    setIsModalOpen(true)
  }

  const closeModal = () => {
    setIsModalOpen(false)
    setModalMode('create')
    setEditingId(null)
    setModalError('')
    setOrgName('')
    setOrgType('')
    setOrgPlan('')
    setOrgStatus('Active')
    setFirstAdminEmail('')
    setGeneratedCode('')
  }

  const saveOrganization = async () => {
    if (!canSave) {
      return
    }
    setModalError('')
    setSaving(true)
    try {
      const isCreate = modalMode === 'create'
      const response = await fetch(
        isCreate
          ? `${apiBase}/api/organizations`
          : `${apiBase}/api/organizations/${editingId}`,
        {
          method: isCreate ? 'POST' : 'PATCH',
          headers: authJsonHeaders(),
          body: JSON.stringify(
            isCreate
              ? {
                  name: orgName.trim(),
                  type: orgType,
                  plan: orgPlan,
                  orgCode: generatedCode,
                  firstAdminEmail: firstAdminEmail.trim(),
                }
              : {
                  name: orgName.trim(),
                  type: orgType,
                  plan: orgPlan,
                  status: orgStatus,
                },
          ),
        },
      )
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setModalError(parseApiErrorMessage(data))
        return
      }
      const list = Array.isArray(data) ? (data as ApiOrganization[]) : []
      setRows(list.map(mapOrgToRow))
      closeModal()
    } catch {
      setModalError('Cannot reach the server.')
    } finally {
      setSaving(false)
    }
  }

  const openBanModal = (row: OrgRow) => {
    setBanModalRow(row)
    setBanPermanent(false)
    setBanMinutes('')
    setBanHours('')
    setBanDays('')
    setBanModalError('')
  }

  const closeBanModal = () => {
    setBanModalRow(null)
    setBanModalError('')
    setBanSaving(false)
  }

  const banDurationTotalMs = useMemo(() => {
    const m = Number.parseInt(banMinutes, 10)
    const h = Number.parseInt(banHours, 10)
    const d = Number.parseInt(banDays, 10)
    const minutes = Number.isFinite(m) && m > 0 ? m : 0
    const hours = Number.isFinite(h) && h > 0 ? h : 0
    const days = Number.isFinite(d) && d > 0 ? d : 0
    return ((days * 24 + hours) * 60 + minutes) * 60 * 1000
  }, [banMinutes, banHours, banDays])

  const canSubmitBan = banPermanent || banDurationTotalMs > 0

  const submitBan = async () => {
    if (!banModalRow || !canSubmitBan) {
      return
    }
    setBanModalError('')
    setBanSaving(true)
    try {
      const body = banPermanent
        ? { permanent: true }
        : {
            minutes: Number.parseInt(banMinutes, 10) || 0,
            hours: Number.parseInt(banHours, 10) || 0,
            days: Number.parseInt(banDays, 10) || 0,
          }
      const response = await fetch(
        `${apiBase}/api/organizations/${banModalRow.id}/license/ban`,
        {
          method: 'POST',
          headers: authJsonHeaders(),
          body: JSON.stringify(body),
        },
      )
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setBanModalError(parseApiErrorMessage(data))
        return
      }
      const list = Array.isArray(data) ? (data as ApiOrganization[]) : []
      setRows(list.map(mapOrgToRow))
      closeBanModal()
    } catch {
      setBanModalError('Cannot reach the server.')
    } finally {
      setBanSaving(false)
    }
  }

  const reactivateLicense = async (row: OrgRow) => {
    const blocked = isLicenseCurrentlyBlocked(row)
    const ok = window.confirm(
      blocked
        ? `Reactivate license for "${row.name}" (code ${row.code})? Clients can use the app again if the org is Active and the subscription is still valid.`
        : `Clear license ban flags for "${row.name}" (code ${row.code})? This removes any stored ban end time from the server.`,
    )
    if (!ok) {
      return
    }
    setReactivatingId(row.id)
    try {
      const response = await fetch(
        `${apiBase}/api/organizations/${row.id}/license/reactivate`,
        {
          method: 'POST',
          headers: authJsonHeaders(),
        },
      )
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setLoadError(parseApiErrorMessage(data))
        return
      }
      const list = Array.isArray(data) ? (data as ApiOrganization[]) : []
      setRows(list.map(mapOrgToRow))
    } catch {
      setLoadError('Cannot reactivate license. Is the API running?')
    } finally {
      setReactivatingId(null)
    }
  }

  const deleteOrganization = async (row: OrgRow) => {
    const ok = window.confirm(
      `Delete organization "${row.name}" (code ${row.code})? This cannot be undone.`,
    )
    if (!ok) {
      return
    }
    setDeletingId(row.id)
    try {
      const response = await fetch(`${apiBase}/api/organizations/${row.id}`, {
        method: 'DELETE',
        headers: authJsonHeaders(),
      })
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setLoadError(parseApiErrorMessage(data))
        return
      }
      const list = Array.isArray(data) ? (data as ApiOrganization[]) : []
      setRows(list.map(mapOrgToRow))
    } catch {
      setLoadError('Cannot delete organization. Is the API running?')
    } finally {
      setDeletingId(null)
    }
  }

  return (
    <section className="organizations-section">
      <div className="org-toolbar">
        <input
          type="text"
          placeholder="Search organizations..."
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <button type="button" className="btn-light" onClick={openCreateModal}>
          + Add Organization
        </button>
      </div>
      {loadError ? <p className="form-error">{loadError}</p> : null}
      {loading ? <p className="admin-muted">Loading organizations…</p> : null}
      <div className="org-table-wrap">
        <table className="org-table">
          <thead>
            <tr>
              <th>Org Code</th>
              <th>Name</th>
              <th>Type</th>
              <th>Plan</th>
              <th>Validity</th>
              <th>Status</th>
              <th>License</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {!loading && filteredRows.length === 0 ? (
              <tr>
                <td colSpan={9} className="org-empty-cell">
                  No organizations yet. Add one to get started.
                </td>
              </tr>
            ) : null}
            {filteredRows.map((row) => (
              <tr key={row.id}>
                <td>{row.code}</td>
                <td>{row.name}</td>
                <td>
                  <span className={typePillClass(row.type)}>{row.type}</span>
                </td>
                <td>
                  <span className={planPillClass(row.plan)}>{row.plan}</span>
                </td>
                <td>{row.validity}</td>
                <td>
                  <span className={statusPillClass(row.status)}>{row.status}</span>
                </td>
                <td>
                  <span className={licensePillClass(row)} title={licensePillLabel(row)}>
                    {licensePillLabel(row)}
                  </span>
                </td>
                <td>{row.created}</td>
                <td className="action-cell">
                  <button
                    type="button"
                    className="btn-icon"
                    aria-label={`Edit ${row.name}`}
                    onClick={() => openEditModal(row)}
                  >
                    ✎
                  </button>
                  <button
                    type="button"
                    className="btn-icon"
                    aria-label={`Ban license ${row.code}`}
                    onClick={() => openBanModal(row)}
                  >
                    ⛔
                  </button>
                  {showLicenseReactivateOption(row) ? (
                    <button
                      type="button"
                      className="btn-icon btn-icon-success"
                      aria-label={`Reactivate license ${row.code}`}
                      disabled={reactivatingId === row.id}
                      onClick={() => void reactivateLicense(row)}
                    >
                      {reactivatingId === row.id ? '…' : '↩'}
                    </button>
                  ) : null}
                  <button
                    type="button"
                    className="btn-icon btn-icon-danger"
                    aria-label={`Delete ${row.name}`}
                    disabled={deletingId === row.id}
                    onClick={() => void deleteOrganization(row)}
                  >
                    {deletingId === row.id ? '…' : '🗑'}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {isModalOpen ? (
        <div className="modal-overlay" role="dialog" aria-modal="true">
          <div className="modal-card">
            <div className="modal-head">
              <div>
                <h3>{modalMode === 'create' ? 'Create New Organization' : 'Edit Organization'}</h3>
                <p>
                  {modalMode === 'create'
                    ? 'Add a new organization to the platform.'
                    : 'Update name, tier, plan, or status. Changing plan resets validity from today.'}
                </p>
              </div>
              <button type="button" className="modal-close" onClick={closeModal}>
                ×
              </button>
            </div>

            <label>Organization Name *</label>
            <input
              value={orgName}
              onChange={(event) => setOrgName(event.target.value)}
              placeholder="Enter organization name"
            />

            <label>Type *</label>
            <select value={orgType} onChange={(event) => setOrgType(event.target.value)}>
              <option value="">Select type</option>
              <option value="standard">Standard — single location / small catalog</option>
              <option value="premium">Premium — multi-branch, advanced ops</option>
              <option value="enterprise">Enterprise — OEM / distributor scale</option>
            </select>

            <label>Plan *</label>
            <select value={orgPlan} onChange={(event) => setOrgPlan(event.target.value)}>
              <option value="">Select plan</option>
              <option value="demo">Demo — 1 day (prospect evaluation)</option>
              <option value="trial">Trial — 14 days full access</option>
              <option value="starter">Starter — monthly (small vendor)</option>
              <option value="professional">Professional — monthly (larger vendor)</option>
              <option value="annual">Annual — yearly subscription</option>
              <option value="lifetime">Lifetime — perpetual (special)</option>
            </select>

            {modalMode === 'edit' ? (
              <>
                <label>Status *</label>
                <select value={orgStatus} onChange={(event) => setOrgStatus(event.target.value)}>
                  <option value="Active">Active</option>
                  <option value="Suspended">Suspended</option>
                </select>
              </>
            ) : null}

            {modalMode === 'create' ? (
              <>
                <label>First Admin Email *</label>
                <input
                  value={firstAdminEmail}
                  onChange={(event) => setFirstAdminEmail(event.target.value)}
                  placeholder="first.admin@company.com"
                  type="email"
                  autoComplete="email"
                />

                <div className="generated-code-row">
                  <label>Organization Code</label>
                  <button type="button" className="btn-dark" onClick={generateCode}>
                    Generate Code
                  </button>
        </div>

                <div className="generated-code-box">
                  {generatedCode ? (
                    <>
                      <span>Generated Code</span>
                      <strong>{generatedCode}</strong>
                    </>
                  ) : (
                    <span>
                      Click &quot;Generate Code&quot; to create a unique 6-digit organization code
                    </span>
                  )}
                </div>
              </>
            ) : (
              <div className="generated-code-box">
                <span>Organization code (read-only)</span>
                <strong>{generatedCode}</strong>
              </div>
            )}

            {modalError ? <p className="form-error">{modalError}</p> : null}

            <div className="modal-actions">
              <button type="button" className="btn-ghost" onClick={closeModal}>
                Cancel
              </button>
              <button
                type="button"
                className="btn-light"
                disabled={!canSave || saving}
                onClick={() => void saveOrganization()}
              >
                {saving
                  ? modalMode === 'create'
                    ? 'Creating…'
                    : 'Saving…'
                  : modalMode === 'create'
                    ? 'Create Organization'
                    : 'Save changes'}
              </button>
            </div>
          </div>
        </div>
      ) : null}
      {banModalRow ? (
        <div className="modal-overlay" role="dialog" aria-modal="true">
          <div className="modal-card">
            <div className="modal-head">
        <div>
                <h3>Ban license</h3>
          <p>
                  Block the Windows client for{' '}
                  <strong>{banModalRow.name}</strong> (code {banModalRow.code}). Subscription
                  status is unchanged; this only affects license verification.
          </p>
        </div>
              <button type="button" className="modal-close" onClick={closeBanModal}>
                ×
              </button>
            </div>

            <label className="ban-check-row">
              <input
                type="checkbox"
                checked={banPermanent}
                onChange={(event) => setBanPermanent(event.target.checked)}
              />
              <span>Permanent ban (must use Reactivate to allow clients again)</span>
            </label>

            {!banPermanent ? (
              <>
                <p className="admin-muted ban-duration-hint">
                  Temporary ban: set one or more of minutes, hours, or days (counts from now).
                </p>
                <div className="ban-duration-grid">
                  <div>
                    <label>Minutes</label>
                    <input
                      type="number"
                      min={0}
                      inputMode="numeric"
                      value={banMinutes}
                      onChange={(event) => setBanMinutes(event.target.value)}
                      placeholder="0"
                    />
                  </div>
                  <div>
                    <label>Hours</label>
                    <input
                      type="number"
                      min={0}
                      inputMode="numeric"
                      value={banHours}
                      onChange={(event) => setBanHours(event.target.value)}
                      placeholder="0"
                    />
                  </div>
                  <div>
                    <label>Days</label>
                    <input
                      type="number"
                      min={0}
                      inputMode="numeric"
                      value={banDays}
                      onChange={(event) => setBanDays(event.target.value)}
                      placeholder="0"
                    />
                  </div>
                </div>
              </>
            ) : null}

            {banModalError ? <p className="form-error">{banModalError}</p> : null}

            <div className="modal-actions">
              <button type="button" className="btn-ghost" onClick={closeBanModal}>
                Cancel
              </button>
        <button
                type="button"
                className="btn-light"
                disabled={!canSubmitBan || banSaving}
                onClick={() => void submitBan()}
              >
                {banSaving ? 'Applying…' : 'Apply ban'}
        </button>
            </div>
          </div>
        </div>
      ) : null}
      </section>
  )
}

type DbClusterRow = {
  id: string
  uriMasked: string
  createdAt: string
  databasesUsed: number
  maxDatabases: number
  databasesFree: number
}

type ClusterAssignedOrgRow = {
  id: string
  orgCode: string
  name: string
  status: string
  createdAt: string
}

function DatabaseClusterOrganizationsSection({
  clusterId,
  onSessionExpired,
}: {
  clusterId: string
  onSessionExpired: () => void
}) {
  const navigate = useNavigate()
  const [orgs, setOrgs] = useState<ClusterAssignedOrgRow[]>([])
  const [uriMasked, setUriMasked] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')

  const load = useCallback(async () => {
    setLoadError('')
    setLoading(true)
    try {
      const headers = authJsonHeaders()
      const [orgsRes, clustersRes] = await Promise.all([
        fetch(`${apiBase}/api/admin/db-clusters/${encodeURIComponent(clusterId)}/organizations`, {
          headers,
        }),
        fetch(`${apiBase}/api/admin/db-clusters`, { headers }),
      ])
      if (orgsRes.status === 401 || clustersRes.status === 401) {
        onSessionExpired()
        return
      }
      if (!clustersRes.ok) {
        setUriMasked(null)
      } else {
        const list: unknown = await clustersRes.json().catch(() => null)
        const rows = Array.isArray(list) ? (list as DbClusterRow[]) : []
        const meta = rows.find((r) => r.id === clusterId)
        setUriMasked(meta?.uriMasked ?? null)
      }
      if (!orgsRes.ok) {
        const data: unknown = await orgsRes.json().catch(() => null)
        setLoadError(parseApiErrorMessage(data))
        setOrgs([])
        return
      }
      const data: unknown = await orgsRes.json().catch(() => null)
      const list = Array.isArray(data) ? (data as ClusterAssignedOrgRow[]) : []
      setOrgs(list)
    } catch {
      setLoadError('Cannot load data. Is the API running?')
      setOrgs([])
      setUriMasked(null)
    } finally {
      setLoading(false)
    }
  }, [clusterId, onSessionExpired])

  useEffect(() => {
    void load()
  }, [load])

  return (
    <section className="organizations-section">
      <div className="org-toolbar">
        <button
          type="button"
          className="btn-ghost"
          onClick={() => navigate('/admin/database-management')}
        >
          Back to clusters
        </button>
        </div>
      {uriMasked ? (
        <p className="admin-muted">
          Cluster connection: <code className="db-cluster-masked">{uriMasked}</code>
        </p>
      ) : null}
      {loadError ? <p className="form-error">{loadError}</p> : null}
      {loading ? <p className="admin-muted">Loading organizations…</p> : null}
      <div className="org-table-wrap">
        <table className="org-table">
          <thead>
            <tr>
              <th>Org code</th>
              <th>Name</th>
              <th>Status</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            {!loading && orgs.length === 0 && !loadError ? (
              <tr>
                <td colSpan={4} className="org-empty-cell">
                  No organizations are assigned to this cluster yet. Organizations appear here after
                  they complete setup with Default database mode.
                </td>
              </tr>
            ) : null}
            {orgs.map((row) => (
              <tr key={row.id}>
                <td>
                  <code>{row.orgCode}</code>
                </td>
                <td>{row.name}</td>
                <td>{row.status}</td>
                <td>{new Date(row.createdAt).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function DatabaseManagementSection({
  onSessionExpired,
}: {
  onSessionExpired: () => void
}) {
  const navigate = useNavigate()
  const [rows, setRows] = useState<DbClusterRow[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [uri, setUri] = useState('')
  const [testPhase, setTestPhase] = useState<'idle' | 'testing' | 'success' | 'error'>(
    'idle',
  )
  const [testMessage, setTestMessage] = useState('')
  const [savePending, setSavePending] = useState(false)
  const [modalError, setModalError] = useState('')

  const loadClusters = useCallback(async () => {
    setLoadError('')
    setLoading(true)
    try {
      const response = await fetch(`${apiBase}/api/admin/db-clusters`, {
        headers: authJsonHeaders(),
      })
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setLoadError(parseApiErrorMessage(data))
        setRows([])
        return
      }
      const list = Array.isArray(data) ? (data as DbClusterRow[]) : []
      setRows(list)
    } catch {
      setLoadError('Cannot load clusters. Is the API running?')
      setRows([])
    } finally {
      setLoading(false)
    }
  }, [onSessionExpired])

  useEffect(() => {
    void loadClusters()
  }, [loadClusters])

  const openModal = () => {
    setModalOpen(true)
    setUri('')
    setTestPhase('idle')
    setTestMessage('')
    setModalError('')
  }

  const closeModal = () => {
    setModalOpen(false)
    setUri('')
    setTestPhase('idle')
    setTestMessage('')
    setModalError('')
  }

  const onUriChange = (value: string) => {
    setUri(value)
    setTestPhase('idle')
    setTestMessage('')
    setModalError('')
  }

  const runTest = async () => {
    const trimmed = uri.trim()
    if (!trimmed) {
      return
    }
    setModalError('')
    setTestPhase('testing')
    setTestMessage('')
    try {
      const response = await fetch(`${apiBase}/api/admin/db-clusters/test`, {
        method: 'POST',
        headers: authJsonHeaders(),
        body: JSON.stringify({ uri: trimmed }),
      })
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setTestPhase('error')
        setTestMessage(parseApiErrorMessage(data))
        return
      }
      const probe = data as { ok?: boolean; message?: string }
      if (probe.ok === true) {
        setTestPhase('success')
        setTestMessage('Connection successful. You can save this cluster.')
        return
      }
      setTestPhase('error')
      setTestMessage(
        typeof probe.message === 'string' && probe.message.trim()
          ? probe.message
          : 'Connection test failed.',
      )
    } catch {
      setTestPhase('error')
      setTestMessage('Cannot reach the server.')
    }
  }

  const saveCluster = async () => {
    const trimmed = uri.trim()
    if (!trimmed || testPhase !== 'success') {
      return
    }
    setModalError('')
    setSavePending(true)
    try {
      const response = await fetch(`${apiBase}/api/admin/db-clusters`, {
        method: 'POST',
        headers: authJsonHeaders(),
        body: JSON.stringify({ uri: trimmed }),
      })
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setModalError(parseApiErrorMessage(data))
        return
      }
      closeModal()
      void loadClusters()
    } catch {
      setModalError('Cannot reach the server. Is the API running?')
    } finally {
      setSavePending(false)
    }
  }

  const uriNonEmpty = uri.trim().length > 0
  const canSave = testPhase === 'success' && uriNonEmpty && !savePending

  return (
    <section className="organizations-section">
      <div className="org-toolbar org-toolbar--end">
        <button type="button" className="btn-light" onClick={openModal}>
          + Add new cluster
        </button>
      </div>
      <p className="admin-muted">
        Right-click a cluster row to open the list of organizations using that cluster.
      </p>
      {loadError ? <p className="form-error">{loadError}</p> : null}
      {loading ? <p className="admin-muted">Loading clusters…</p> : null}
      <div className="org-table-wrap">
        <table className="org-table">
          <thead>
            <tr>
              <th>Connection (masked)</th>
              <th>Tenant databases</th>
              <th>Added</th>
            </tr>
          </thead>
          <tbody>
            {!loading && rows.length === 0 ? (
              <tr>
                <td colSpan={3} className="org-empty-cell">
                  No clusters yet. Add a MongoDB URI to register a cluster.
                </td>
              </tr>
            ) : null}
            {rows.map((row) => (
              <tr
                key={row.id}
                className="db-cluster-row"
                title="Right-click to view organizations on this cluster"
                onContextMenu={(event) => {
                  event.preventDefault()
                  navigate(`/admin/database-management/cluster/${row.id}`)
                }}
              >
                <td>
                  <code className="db-cluster-masked">{row.uriMasked}</code>
                </td>
                <td className="admin-muted">
                  {row.databasesUsed} / {row.maxDatabases} used ·{' '}
                  {row.databasesFree} free
                </td>
                <td>{new Date(row.createdAt).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {modalOpen ? (
        <div className="modal-overlay" role="dialog" aria-modal="true">
          <div className="modal-card modal-card--wide">
            <div className="modal-head">
              <div>
                <h3>Add new cluster</h3>
                <p>Paste the MongoDB URI for this cluster, test the connection, then save.</p>
              </div>
              <button type="button" className="modal-close" onClick={closeModal}>
                ×
              </button>
            </div>

            <label htmlFor="db-cluster-uri">MongoDB URI</label>
            <textarea
              id="db-cluster-uri"
              className="db-cluster-uri-input"
              rows={4}
              placeholder="mongodb://… or mongodb+srv://…"
              value={uri}
              onChange={(e) => onUriChange(e.target.value)}
              autoComplete="off"
              spellCheck={false}
            />

            <div>
              <button
                type="button"
                className="btn-light"
                disabled={!uriNonEmpty || testPhase === 'testing'}
                onClick={() => void runTest()}
              >
                {testPhase === 'testing' ? 'Testing…' : 'Test connection'}
              </button>
            </div>

            {testMessage ? (
              <p
                className={
                  testPhase === 'success' ? 'form-success' : testPhase === 'error' ? 'form-error' : 'admin-muted'
                }
              >
                {testMessage}
              </p>
            ) : null}
            {modalError ? <p className="form-error">{modalError}</p> : null}

            <div className="modal-actions">
              <button type="button" className="btn-ghost" onClick={closeModal}>
                Cancel
              </button>
              {testPhase === 'success' ? (
                <button
                  type="button"
                  className="btn-light"
                  disabled={!canSave}
                  onClick={() => void saveCluster()}
                >
                  {savePending ? 'Saving…' : 'Save'}
                </button>
              ) : null}
        </div>
          </div>
        </div>
      ) : null}
      </section>
  )
}

function UsersSection() {
  return (
    <section className="card admin-section">
      <h3>User Management</h3>
      <p>Manage admin users, roles, and account access from this section.</p>
    </section>
  )
}

function isoToLocalDatetimeInputValue(iso: string | null | undefined): string {
  if (!iso) return ''
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return ''
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function SettingsSection({
  onSessionExpired,
}: {
  onSessionExpired: () => void
}) {
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmNewPassword, setConfirmNewPassword] = useState('')
  const [changeError, setChangeError] = useState('')
  const [changeOk, setChangeOk] = useState('')
  const [changePending, setChangePending] = useState(false)

  const [newAdminEmail, setNewAdminEmail] = useState('')
  const [newAdminPassword, setNewAdminPassword] = useState('')
  const [newAdminConfirm, setNewAdminConfirm] = useState('')
  const [createError, setCreateError] = useState('')
  const [createOk, setCreateOk] = useState('')
  const [createPending, setCreatePending] = useState(false)

  const [maintEnabled, setMaintEnabled] = useState(false)
  const [maintUntilLocal, setMaintUntilLocal] = useState('')
  const [maintError, setMaintError] = useState('')
  const [maintOk, setMaintOk] = useState('')
  const [maintPending, setMaintPending] = useState(false)

  useEffect(() => {
    let cancelled = false
    async function load() {
      try {
        const response = await fetch(`${apiBase}/api/admin/platform/maintenance`, {
          headers: authJsonHeaders(),
        })
        if (response.status === 401) {
          onSessionExpired()
          return
        }
        if (!response.ok) return
        const data: unknown = await response.json().catch(() => null)
        if (cancelled || !data || typeof data !== 'object') return
        const enabled = Boolean((data as { maintenanceEnabled?: boolean }).maintenanceEnabled)
        const until = (data as { maintenanceUntil?: string | null }).maintenanceUntil
        setMaintEnabled(enabled)
        setMaintUntilLocal(
          until ? isoToLocalDatetimeInputValue(until) : '',
        )
      } catch {
        // ignore
      }
    }
    void load()
    return () => {
      cancelled = true
    }
  }, [onSessionExpired])

  const submitChangePassword = async () => {
    setChangeError('')
    setChangeOk('')
    if (!currentPassword.trim() || !newPassword.trim()) {
      setChangeError('Enter current and new password.')
      return
    }
    if (newPassword.length < 8) {
      setChangeError('New password must be at least 8 characters.')
      return
    }
    if (newPassword !== confirmNewPassword) {
      setChangeError('New password and confirmation do not match.')
      return
    }
    setChangePending(true)
    try {
      const response = await fetch(`${apiBase}/api/auth/admin/change-password`, {
        method: 'POST',
        headers: authJsonHeaders(),
        body: JSON.stringify({
          currentPassword,
          newPassword,
        }),
      })
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setChangeError(parseApiErrorMessage(data))
        return
      }
      setChangeOk('Password updated successfully.')
      setCurrentPassword('')
      setNewPassword('')
      setConfirmNewPassword('')
    } catch {
      setChangeError('Cannot reach the server. Is the API running?')
    } finally {
      setChangePending(false)
    }
  }

  const submitCreateAdmin = async () => {
    setCreateError('')
    setCreateOk('')
    if (!newAdminEmail.trim() || !newAdminPassword.trim()) {
      setCreateError('Enter email and password.')
      return
    }
    if (newAdminPassword.length < 8) {
      setCreateError('Password must be at least 8 characters.')
      return
    }
    if (newAdminPassword !== newAdminConfirm) {
      setCreateError('Password and confirmation do not match.')
      return
    }
    setCreatePending(true)
    try {
      const response = await fetch(`${apiBase}/api/auth/admin/admins`, {
        method: 'POST',
        headers: authJsonHeaders(),
        body: JSON.stringify({
          email: newAdminEmail.trim(),
          password: newAdminPassword,
        }),
      })
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setCreateError(parseApiErrorMessage(data))
        return
      }
      const email = (data as { user?: { email?: string } }).user?.email
      setCreateOk(
        email ? `Admin created: ${email}` : 'Admin user created successfully.',
      )
      setNewAdminEmail('')
      setNewAdminPassword('')
      setNewAdminConfirm('')
    } catch {
      setCreateError('Cannot reach the server. Is the API running?')
    } finally {
      setCreatePending(false)
    }
  }

  const saveMaintenance = async () => {
    setMaintError('')
    setMaintOk('')
    if (maintEnabled) {
      if (!maintUntilLocal.trim()) {
        setMaintError('Set an end date and time for the maintenance window.')
        return
      }
      const end = new Date(maintUntilLocal)
      if (Number.isNaN(end.getTime())) {
        setMaintError('Invalid date or time.')
        return
      }
      if (end.getTime() <= Date.now()) {
        setMaintError('End time must be in the future.')
        return
      }
    }
    setMaintPending(true)
    try {
      const body: { maintenanceEnabled: boolean; maintenanceUntil?: string } = {
        maintenanceEnabled: maintEnabled,
      }
      if (maintEnabled) {
        body.maintenanceUntil = new Date(maintUntilLocal).toISOString()
      }
      const response = await fetch(`${apiBase}/api/admin/platform/maintenance`, {
        method: 'PATCH',
        headers: authJsonHeaders(),
        body: JSON.stringify(body),
      })
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setMaintError(parseApiErrorMessage(data))
        return
      }
      setMaintOk('Maintenance settings saved.')
      if (data && typeof data === 'object') {
        const enabled = Boolean((data as { maintenanceEnabled?: boolean }).maintenanceEnabled)
        const until = (data as { maintenanceUntil?: string | null }).maintenanceUntil
        setMaintEnabled(enabled)
        setMaintUntilLocal(
          until ? isoToLocalDatetimeInputValue(until) : '',
        )
      }
    } catch {
      setMaintError('Cannot reach the server. Is the API running?')
    } finally {
      setMaintPending(false)
    }
  }

  return (
    <section className="settings-grid">
      <article className="card dark-card">
        <h3>Change Password</h3>
        <p>Update your admin account password</p>
        <label htmlFor="settings-current-pw">Current Password</label>
        <input
          id="settings-current-pw"
          placeholder="Enter current password"
          type="password"
          autoComplete="current-password"
          value={currentPassword}
          onChange={(e) => setCurrentPassword(e.target.value)}
        />
        <label htmlFor="settings-new-pw">New Password</label>
        <input
          id="settings-new-pw"
          placeholder="Enter new password (min 8 characters)"
          type="password"
          autoComplete="new-password"
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
        />
        <label htmlFor="settings-confirm-pw">Confirm New Password</label>
        <input
          id="settings-confirm-pw"
          placeholder="Confirm new password"
          type="password"
          autoComplete="new-password"
          value={confirmNewPassword}
          onChange={(e) => setConfirmNewPassword(e.target.value)}
        />
        {changeError ? <p className="form-error">{changeError}</p> : null}
        {changeOk ? <p className="admin-muted">{changeOk}</p> : null}
        <button
          type="button"
          className="btn-light full-width"
          disabled={changePending}
          onClick={() => void submitChangePassword()}
        >
          {changePending ? 'Updating…' : 'Change Password'}
        </button>
      </article>

      <article className="card dark-card">
        <h3>Add New Admin</h3>
        <p>Create a new admin user who can access the admin panel</p>
        <label htmlFor="settings-admin-email">Email Address</label>
        <input
          id="settings-admin-email"
          placeholder="Enter email address"
          type="email"
          autoComplete="email"
          value={newAdminEmail}
          onChange={(e) => setNewAdminEmail(e.target.value)}
        />
        <label htmlFor="settings-admin-pw">Password</label>
        <input
          id="settings-admin-pw"
          placeholder="Enter password (min 8 characters)"
          type="password"
          autoComplete="new-password"
          value={newAdminPassword}
          onChange={(e) => setNewAdminPassword(e.target.value)}
        />
        <label htmlFor="settings-admin-confirm">Confirm Password</label>
        <input
          id="settings-admin-confirm"
          placeholder="Confirm password"
          type="password"
          autoComplete="new-password"
          value={newAdminConfirm}
          onChange={(e) => setNewAdminConfirm(e.target.value)}
        />
        {createError ? <p className="form-error">{createError}</p> : null}
        {createOk ? <p className="admin-muted">{createOk}</p> : null}
        <button
          type="button"
          className="btn-light full-width"
          disabled={createPending}
          onClick={() => void submitCreateAdmin()}
        >
          {createPending ? 'Creating…' : 'Create Admin User'}
        </button>
      </article>

      <article className="card dark-card settings-maintenance-card">
        <h3>Maintenance window</h3>
        <p>
          When enabled, the PartFinder Windows app shows an under-maintenance screen with a
          countdown until the end time. Turn off to end maintenance immediately.
        </p>
        <label className="maint-checkbox-row">
          <input
            type="checkbox"
            checked={maintEnabled}
            onChange={(e) => {
              const next = e.target.checked
              setMaintEnabled(next)
              if (next && !maintUntilLocal.trim()) {
                const d = new Date(Date.now() + 60 * 60 * 1000)
                setMaintUntilLocal(isoToLocalDatetimeInputValue(d.toISOString()))
              }
            }}
          />
          <span>Maintenance window active</span>
        </label>
        {maintEnabled ? (
          <>
            <label htmlFor="maint-until">End date and time (local)</label>
            <input
              id="maint-until"
              type="datetime-local"
              value={maintUntilLocal}
              onChange={(e) => setMaintUntilLocal(e.target.value)}
            />
          </>
        ) : null}
        {maintError ? <p className="form-error">{maintError}</p> : null}
        {maintOk ? <p className="admin-muted">{maintOk}</p> : null}
        <button
          type="button"
          className="btn-light full-width"
          disabled={maintPending}
          onClick={() => void saveMaintenance()}
        >
          {maintPending ? 'Saving…' : 'Save maintenance settings'}
        </button>
      </article>
    </section>
  )
}

type DebugLogEntry = {
  id: string
  ts: string
  source: string
  level: string
  message: string
  context?: string
}

function formatArgs(args: unknown[]): string {
  return args
    .map((a) => {
      if (typeof a === 'string') return a
      try {
        return JSON.stringify(a)
      } catch {
        return String(a)
      }
    })
    .join(' ')
}

function DebugSection({
  onSessionExpired,
}: {
  onSessionExpired: () => void
}) {
  const [logs, setLogs] = useState<DebugLogEntry[]>([])
  const [filterSource, setFilterSource] = useState<string>('all')
  const [filterLevel, setFilterLevel] = useState<string>('all')
  const [loadError, setLoadError] = useState('')
  const [paused, setPaused] = useState(false)
  const [clearPending, setClearPending] = useState(false)
  const portfolioQueueRef = useRef<
    { source: 'portfolio'; level: string; message: string; context?: string }[]
  >([])
  const flushTimerRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const fetchLogs = useCallback(async () => {
    setLoadError('')
    try {
      const response = await fetch(`${apiBase}/api/admin/debug/logs`, {
        headers: authJsonHeaders(),
      })
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      const data: unknown = await response.json().catch(() => null)
      if (!response.ok) {
        setLoadError(parseApiErrorMessage(data))
        return
      }
      const list = (data as { logs?: DebugLogEntry[] }).logs
      setLogs(Array.isArray(list) ? list : [])
    } catch {
      setLoadError('Cannot reach the server. Is the API running?')
    }
  }, [onSessionExpired])

  useEffect(() => {
    if (paused) {
      return
    }
    void fetchLogs()
    const id = window.setInterval(() => void fetchLogs(), 2500)
    return () => window.clearInterval(id)
  }, [fetchLogs, paused])

  const flushPortfolio = useCallback(async () => {
    const batch = portfolioQueueRef.current.splice(0, portfolioQueueRef.current.length)
    if (batch.length === 0) return
    try {
      await fetch(`${apiBase}/api/admin/debug/logs`, {
        method: 'POST',
        headers: authJsonHeaders(),
        body: JSON.stringify({ items: batch }),
      })
    } catch {
      portfolioQueueRef.current.unshift(...batch)
    }
  }, [])

  useEffect(() => {
    const methods = ['log', 'info', 'warn', 'error', 'debug'] as const
    const originals: Partial<Record<(typeof methods)[number], typeof console.log>> = {}
    for (const m of methods) {
      originals[m] = console[m].bind(console)
    }
    const capture = (level: string, args: unknown[]) => {
      portfolioQueueRef.current.push({
        source: 'portfolio',
        level,
        message: formatArgs(args),
      })
    }
    console.log = (...a: unknown[]) => {
      originals.log?.(...a)
      capture('log', a)
    }
    console.info = (...a: unknown[]) => {
      originals.info?.(...a)
      capture('info', a)
    }
    console.warn = (...a: unknown[]) => {
      originals.warn?.(...a)
      capture('warn', a)
    }
    console.error = (...a: unknown[]) => {
      originals.error?.(...a)
      capture('error', a)
    }
    console.debug = (...a: unknown[]) => {
      originals.debug?.(...a)
      capture('debug', a)
    }
    flushTimerRef.current = window.setInterval(() => void flushPortfolio(), 3000)
    return () => {
      if (flushTimerRef.current) {
        window.clearInterval(flushTimerRef.current)
        flushTimerRef.current = null
      }
      console.log = originals.log ?? console.log
      console.info = originals.info ?? console.info
      console.warn = originals.warn ?? console.warn
      console.error = originals.error ?? console.error
      console.debug = originals.debug ?? console.debug
      void flushPortfolio()
    }
  }, [flushPortfolio])

  const handleClear = async () => {
    setClearPending(true)
    setLoadError('')
    try {
      const response = await fetch(`${apiBase}/api/admin/debug/logs`, {
        method: 'DELETE',
        headers: authJsonHeaders(),
      })
      if (response.status === 401) {
        onSessionExpired()
        return
      }
      if (!response.ok) {
        const data: unknown = await response.json().catch(() => null)
        setLoadError(parseApiErrorMessage(data))
        return
      }
      await fetchLogs()
    } catch {
      setLoadError('Cannot reach the server.')
    } finally {
      setClearPending(false)
    }
  }

  const filtered = useMemo(() => {
    return logs.filter((entry) => {
      if (filterSource !== 'all' && entry.source !== filterSource) {
        return false
      }
      if (filterLevel !== 'all' && entry.level !== filterLevel) {
        return false
      }
      return true
    })
  }, [logs, filterSource, filterLevel])

  const levels = useMemo(() => {
    const s = new Set<string>()
    for (const e of logs) s.add(e.level)
    return ['all', ...Array.from(s).sort()]
  }, [logs])

  return (
    <section className="debug-wrap">
      <div className="debug-toolbar card dark-card">
        <label className="debug-toolbar-field">
          <span>Source</span>
          <select
            value={filterSource}
            onChange={(e) => setFilterSource(e.target.value)}
            className="debug-select"
          >
            <option value="all">All</option>
            <option value="backend">Backend API</option>
            <option value="portfolio">Admin (browser)</option>
            <option value="partfinder-desktop">PartFinder app</option>
          </select>
        </label>
        <label className="debug-toolbar-field">
          <span>Level</span>
          <select
            value={filterLevel}
            onChange={(e) => setFilterLevel(e.target.value)}
            className="debug-select"
          >
            {levels.map((lv) => (
              <option key={lv} value={lv}>
                {lv === 'all' ? 'All' : lv}
              </option>
            ))}
          </select>
        </label>
        <label className="debug-toolbar-toggle">
          <input
            type="checkbox"
            checked={paused}
            onChange={(e) => setPaused(e.target.checked)}
          />
          Pause refresh
        </label>
        <button
          type="button"
          className="btn-light"
          onClick={() => void fetchLogs()}
          disabled={paused}
        >
          Refresh now
        </button>
        <button
          type="button"
          className="btn-light debug-clear-btn"
          onClick={() => void handleClear()}
          disabled={clearPending}
        >
          {clearPending ? 'Clearing…' : 'Clear server log'}
        </button>
      </div>
      {loadError ? <p className="form-error">{loadError}</p> : null}
      <p className="debug-hint">
        Streams Nest logs and HTTP traffic from the API, browser console from this admin
        tab, and (when configured) the PartFinder Windows app via ingest key.
      </p>
      <div className="debug-list">
        {filtered.length === 0 ? (
          <article className="debug-item debug-item-empty">
            <p>No log lines match the current filters.</p>
          </article>
        ) : null}
        {filtered.map((log) => (
          <article key={log.id} className="debug-item">
            <div className="debug-item-head">
              <span className={`debug-badge debug-src-${log.source}`}>
                {log.source}
              </span>
              <time dateTime={log.ts}>{log.ts}</time>
              <strong className="debug-level">{log.level}</strong>
            </div>
            <p className="debug-msg">{log.message}</p>
            {log.context ? (
              <pre className="debug-ctx">{log.context}</pre>
            ) : null}
          </article>
        ))}
      </div>
    </section>
  )
}

function ReleasesSection() {
  return (
    <section className="releases-wrap">
      <div className="release-kpis">
        <article className="card dark-card">
          <h4>Total Releases</h4>
          <p>2</p>
        </article>
        <article className="card dark-card">
          <h4>Latest Version</h4>
          <p>3.0.0</p>
        </article>
        <article className="card dark-card">
          <h4>Stable Releases</h4>
          <p>2</p>
        </article>
        <article className="card dark-card">
          <h4>Beta Releases</h4>
          <p>0</p>
        </article>
      </div>

      <article className="card dark-card release-card">
        <h3>v3.0.0</h3>
        <p>Release notes and package links for latest production release.</p>
        <ul>
          <li>Windows installer support</li>
          <li>MSIX package pipeline</li>
          <li>Silent installation support</li>
        </ul>
      </article>
    </section>
  )
}

function NotFoundPage() {
  return (
    <main className="page">
      <section className="card">
        <h2>Page not found</h2>
        <p>The route you requested does not exist.</p>
        <a href="/" className="btn-secondary">
          Back to Home
        </a>
      </section>
    </main>
  )
}

export default App
