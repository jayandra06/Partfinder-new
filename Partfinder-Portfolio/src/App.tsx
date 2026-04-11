import { useCallback, useEffect, useMemo, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, NavLink, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom'

const ADMIN_TOKEN_KEY = 'pf_admin_token'

const apiBase =
  import.meta.env.VITE_API_URL?.replace(/\/$/, '') ?? 'http://localhost:3000'

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
        {isSettingsPage ? <SettingsSection /> : null}
        {isDebugPage ? <DebugSection /> : null}
        {isReleasesPage ? <ReleasesSection /> : null}
        {!isOrganizationsPage &&
        !isUsersPage &&
        !isSettingsPage &&
        !isDebugPage &&
        !isReleasesPage ? (
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
  createdAt: string
}

type OrgRow = {
  code: string
  name: string
  type: string
  plan: string
  validity: string
  status: string
  created: string
}

function mapOrgToRow(o: ApiOrganization): OrgRow {
  const validity = o.validity ? new Date(o.validity).toISOString().slice(0, 10) : '—'
  const created = o.createdAt
    ? new Date(o.createdAt).toLocaleDateString()
    : '—'
  return {
    code: o.orgCode,
    name: o.name,
    type: o.type,
    plan: o.plan,
    validity,
    status: o.status,
    created,
  }
}

function OrganizationsSection({
  onSessionExpired,
}: {
  onSessionExpired: () => void
}) {
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [orgName, setOrgName] = useState('')
  const [orgType, setOrgType] = useState('')
  const [orgPlan, setOrgPlan] = useState('')
  const [generatedCode, setGeneratedCode] = useState('')
  const [search, setSearch] = useState('')
  const [rows, setRows] = useState<OrgRow[]>([])
  const [loadError, setLoadError] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [modalError, setModalError] = useState('')

  useEffect(() => {
    let cancelled = false
    const run = async () => {
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
          if (!cancelled) {
            setLoadError(parseApiErrorMessage(data))
          }
          return
        }
        const list = Array.isArray(data) ? (data as ApiOrganization[]) : []
        if (!cancelled) {
          setRows(list.map(mapOrgToRow))
        }
      } catch {
        if (!cancelled) {
          setLoadError('Cannot load organizations. Is the API running?')
        }
      } finally {
        if (!cancelled) {
          setLoading(false)
        }
      }
    }
    void run()
    return () => {
      cancelled = true
    }
  }, [onSessionExpired])

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

  const canCreate = useMemo(() => {
    return Boolean(orgName.trim() && orgType && orgPlan && generatedCode)
  }, [orgName, orgType, orgPlan, generatedCode])

  const generateCode = () => {
    const code = Math.floor(100000 + Math.random() * 900000).toString()
    setGeneratedCode(code)
  }

  const closeModal = () => {
    setIsModalOpen(false)
    setModalError('')
    setOrgName('')
    setOrgType('')
    setOrgPlan('')
    setGeneratedCode('')
  }

  const createOrganization = async () => {
    if (!canCreate) {
      return
    }
    setModalError('')
    setSaving(true)
    try {
      const response = await fetch(`${apiBase}/api/organizations`, {
        method: 'POST',
        headers: authJsonHeaders(),
        body: JSON.stringify({
          name: orgName.trim(),
          type: orgType,
          plan: orgPlan,
          orgCode: generatedCode,
        }),
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
      const list = Array.isArray(data) ? (data as ApiOrganization[]) : []
      setRows(list.map(mapOrgToRow))
      closeModal()
    } catch {
      setModalError('Cannot reach the server.')
    } finally {
      setSaving(false)
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
        <button type="button" className="btn-light" onClick={() => setIsModalOpen(true)}>
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
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {!loading && filteredRows.length === 0 ? (
              <tr>
                <td colSpan={8} className="org-empty-cell">
                  No organizations yet. Add one to get started.
                </td>
              </tr>
            ) : null}
            {filteredRows.map((row) => (
              <tr key={row.code}>
                <td>{row.code}</td>
                <td>{row.name}</td>
                <td>
                  <span
                    className={
                      row.type === 'premium' ? 'pill pill-premium' : 'pill pill-standard'
                    }
                  >
                    {row.type}
                  </span>
                </td>
                <td>{row.plan}</td>
                <td>{row.validity}</td>
                <td>
                  <span className="pill pill-active">{row.status}</span>
                </td>
                <td>{row.created}</td>
                <td className="action-cell">✎ 🗑</td>
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
                <h3>Create New Organization</h3>
                <p>Add a new organization to the platform.</p>
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
              <option value="premium">premium</option>
              <option value="standard">standard</option>
            </select>

            <label>Plan *</label>
            <select value={orgPlan} onChange={(event) => setOrgPlan(event.target.value)}>
              <option value="">Select plan</option>
              <option value="lifetime">lifetime</option>
              <option value="annual">annual</option>
            </select>

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
                <span>Click &quot;Generate Code&quot; to create a unique 6-digit organization code</span>
              )}
            </div>

            {modalError ? <p className="form-error">{modalError}</p> : null}

            <div className="modal-actions">
              <button type="button" className="btn-ghost" onClick={closeModal}>
                Cancel
              </button>
              <button
                type="button"
                className="btn-light"
                disabled={!canCreate || saving}
                onClick={() => void createOrganization()}
              >
                {saving ? 'Creating…' : 'Create Organization'}
              </button>
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

function SettingsSection() {
  return (
    <section className="settings-grid">
      <article className="card dark-card">
        <h3>Change Password</h3>
        <p>Update your admin account password</p>
        <label>Current Password</label>
        <input placeholder="Enter current password" type="password" />
        <label>New Password</label>
        <input placeholder="Enter new password" type="password" />
        <label>Confirm New Password</label>
        <input placeholder="Confirm new password" type="password" />
        <button type="button" className="btn-light full-width">
          Change Password
        </button>
      </article>

      <article className="card dark-card">
        <h3>Add New Admin</h3>
        <p>Create a new admin user who can access the admin panel</p>
        <label>Email Address</label>
        <input placeholder="Enter email address" type="email" />
        <label>Password</label>
        <input placeholder="Enter password" type="password" />
        <label>Confirm Password</label>
        <input placeholder="Confirm password" type="password" />
        <button type="button" className="btn-light full-width">
          Create Admin User
        </button>
      </article>
    </section>
  )
}

function DebugSection() {
  const logs = [
    { level: 'INFO', message: 'Initializing MongoDB client in production mode' },
    { level: 'INFO', message: 'API call initiated /api/organizations' },
    { level: 'DEBUG', message: 'Database operation: connect on collection partfinder' },
    { level: 'INFO', message: 'MongoDB client connected successfully in production mode' },
  ]

  return (
    <section className="debug-wrap">
      <div className="debug-toolbar card dark-card">
        <span>Filters</span>
        <span>Level</span>
        <span>Operation</span>
      </div>
      <div className="debug-list">
        {logs.map((log, index) => (
          <article key={`${log.level}-${index}`} className="debug-item">
            <strong>{log.level}</strong>
            <p>{log.message}</p>
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
