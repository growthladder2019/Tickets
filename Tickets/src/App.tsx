import { type FormEvent, type ReactNode, useEffect, useState } from 'react'

type TicketStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed'
type AdminTab = 'applications' | 'users' | 'tickets'

type AuthResponse = {
  accessToken: string
  refreshToken: string
  expiresUtc: string
  email: string
  displayName: string
  appCode: string
  isSuperAdmin: boolean
}

type TicketSummary = {
  id: string
  ticketNumber: string
  title: string
  description: string
  category: string
  priority: string
  assignedAgentEmail: string | null
  assignedAtUtc: string | null
  status: TicketStatus
  createdUtc: string
  updatedUtc: string
}

type TicketDetail = {
  id: string
  ticketNumber: string
  requesterName: string
  title: string
  description: string
  message: string
  category: string
  priority: string
  assignedAgentEmail: string | null
  assignedAtUtc: string | null
  status: TicketStatus
  createdUtc: string
  updatedUtc: string
  history: Array<{ id: string; eventType: string; description: string; actorEmail: string; createdUtc: string }>
  attachments: Array<{
    id: string
    fileName: string
    contentType: string
    fileSizeBytes: number
    isImage: boolean
    blobPath: string
    uploadedByEmail: string
    uploadedUtc: string
  }>
}

type ApplicationAdmin = {
  id: string
  code: string
  name: string
  createdUtc: string
}

type UserAdmin = {
  id: string
  email: string
  displayName: string
  isSuperAdmin: boolean
  createdUtc: string
  appCodes: string[]
}

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'https://mindticketmanagement-fna0gsfmfjecfrbe.centralindia-01.azurewebsites.net'
const AUTH_STORAGE_KEY = 'ticket-system-auth-v2'
const CATEGORY_OPTIONS = ['General', 'Bug', 'Feature Request', 'Access', 'Billing', 'Other'] as const
const PRIORITY_OPTIONS = ['Low', 'Medium', 'High', 'Critical'] as const
const GLOBAL_LOADER_EVENT = 'ticket-system-global-loader'

function startGlobalLoader() {
  window.dispatchEvent(new CustomEvent(GLOBAL_LOADER_EVENT, { detail: { action: 'start' } }))
}

function stopGlobalLoader() {
  window.dispatchEvent(new CustomEvent(GLOBAL_LOADER_EVENT, { detail: { action: 'stop' } }))
}

function toStatusLabel(status: TicketStatus): string {
  if (status === 'InProgress') {
    return 'In Progress'
  }

  return status
}

function getStatusBadgeClass(status: TicketStatus): string {
  switch (status) {
    case 'Open':
      return 'status-open'
    case 'InProgress':
      return 'status-in-progress'
    case 'Resolved':
      return 'status-resolved'
    case 'Closed':
      return 'status-closed'
    default:
      return ''
  }
}

function getTicketStatusCounts(tickets: TicketSummary[]): Record<TicketStatus, number> {
  return tickets.reduce<Record<TicketStatus, number>>(
    (counts, ticket) => {
      counts[ticket.status] += 1
      return counts
    },
    {
      Open: 0,
      InProgress: 0,
      Resolved: 0,
      Closed: 0
    }
  )
}

function TicketStatusSummaryBand({ title, tickets }: { title: string; tickets: TicketSummary[] }) {
  const counts = getTicketStatusCounts(tickets)

  return (
    <section className="status-summary-band" aria-label="Ticket status summary">
      <div className="status-summary-header">
        <h3>{title}</h3>
        <span>Total: {tickets.length}</span>
      </div>
      <div className="status-summary-grid">
        <article className="status-summary-item status-open">
          <p>Open</p>
          <strong>{counts.Open}</strong>
        </article>
        <article className="status-summary-item status-in-progress">
          <p>In Progress</p>
          <strong>{counts.InProgress}</strong>
        </article>
        <article className="status-summary-item status-resolved">
          <p>Resolved</p>
          <strong>{counts.Resolved}</strong>
        </article>
        <article className="status-summary-item status-closed">
          <p>Closed</p>
          <strong>{counts.Closed}</strong>
        </article>
      </div>
    </section>
  )
}

async function apiRequest<T>(path: string, init?: RequestInit, token?: string): Promise<T> {
  const headers = new Headers(init?.headers)
  if (!headers.has('Content-Type') && !(init?.body instanceof FormData)) {
    headers.set('Content-Type', 'application/json')
  }

  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const normalizedBaseUrl = API_BASE_URL.replace(/\/+$/, '')
  const normalizedPath = path.startsWith('/') ? path : `/${path}`
  const requestUrl = `${normalizedBaseUrl}${normalizedPath}`

  startGlobalLoader()
  try {
    const response = await fetch(requestUrl, { ...init, headers })
    if (!response.ok) {
      const text = await response.text()
      throw new Error(text || `Request failed with status ${response.status}`)
    }

    if (response.status === 204) {
      return undefined as T
    }

    return (await response.json()) as T
  } finally {
    stopGlobalLoader()
  }
}

function getInitialSiteId(): string | null {
  const params = new URLSearchParams(window.location.search)
  const fromQuery = params.get('siteId') ?? params.get('siteid')
  if (fromQuery && fromQuery.trim()) {
    return fromQuery.trim()
  }

  return null
}

function App() {
  const [auth, setAuth] = useState<AuthResponse | null>(null)
  const [pendingRequestCount, setPendingRequestCount] = useState(0)

  useEffect(() => {
    const stored = localStorage.getItem(AUTH_STORAGE_KEY)
    if (!stored) {
      return
    }

    try {
      setAuth(JSON.parse(stored) as AuthResponse)
    } catch {
      localStorage.removeItem(AUTH_STORAGE_KEY)
    }
  }, [])

  useEffect(() => {
    const onLoaderEvent = (event: Event) => {
      const loaderEvent = event as CustomEvent<{ action?: string }>
      if (loaderEvent.detail?.action === 'start') {
        setPendingRequestCount((count) => count + 1)
        return
      }

      if (loaderEvent.detail?.action === 'stop') {
        setPendingRequestCount((count) => (count > 0 ? count - 1 : 0))
      }
    }

    window.addEventListener(GLOBAL_LOADER_EVENT, onLoaderEvent)
    return () => window.removeEventListener(GLOBAL_LOADER_EVENT, onLoaderEvent)
  }, [])

  const logout = () => {
    setAuth(null)
    localStorage.removeItem(AUTH_STORAGE_KEY)
  }

  let content: ReactNode
  if (!auth) {
    const isSuperAdminRoute = window.location.pathname.toLowerCase().includes('super-admin')
    content = (
      <main className="auth-shell">
        <AuthPanel
          siteId={isSuperAdminRoute ? null : getInitialSiteId()}
          isSuperAdminMode={isSuperAdminRoute}
          onAuthenticated={(next) => {
            setAuth(next)
            localStorage.setItem(AUTH_STORAGE_KEY, JSON.stringify(next))
          }}
        />
      </main>
    )
  } else if (auth.isSuperAdmin) {
    content = <SuperAdminPortal auth={auth} onLogout={logout} />
  } else {
    content = <UserPortal auth={auth} onLogout={logout} />
  }

  return (
    <>
      {content}
      {pendingRequestCount > 0 ? (
        <div className="global-loader-overlay" role="status" aria-live="polite" aria-label="Loading">
          <div className="global-loader-spinner" />
        </div>
      ) : null}
    </>
  )
}

function AuthPanel({
  siteId,
  isSuperAdminMode,
  onAuthenticated
}: {
  siteId: string | null
  isSuperAdminMode: boolean
  onAuthenticated: (auth: AuthResponse) => void
}) {
  const [mode, setMode] = useState<'login' | 'register'>(isSuperAdminMode ? 'login' : 'login')
  const [appCode, setAppCode] = useState('MINDWEAVE-CORE')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError('')

    try {
      if (!isSuperAdminMode && !siteId) {
        setError('Missing siteId in login URL. Open this page with ?siteId=<application-guid>.')
        return
      }

      if (!isSuperAdminMode && mode === 'register') {
        await apiRequest<AuthResponse>('/api/auth/register', {
          method: 'POST',
          body: JSON.stringify({ appCode, email, password, displayName })
        })
      }

      const auth = await apiRequest<AuthResponse>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ siteId: isSuperAdminMode ? null : siteId, email, password })
      })
      onAuthenticated(auth)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Login failed.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="auth-card">
      <h2>{isSuperAdminMode ? 'Super Admin Login' : mode === 'login' ? 'User Login' : 'User Registration'}</h2>
      <form className="ticket-form" onSubmit={submit}>
        {!isSuperAdminMode && mode === 'register' ? (
          <label>
            Application code
            <input value={appCode} onChange={(event) => setAppCode(event.target.value)} required />
          </label>
        ) : null}
        {!isSuperAdminMode ? <p>Site ID: {siteId ?? 'Missing in URL'}</p> : null}
        <label>
          Email
          <input type="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
        </label>
        {!isSuperAdminMode && mode === 'register' ? (
          <label>
            User name
            <input value={displayName} onChange={(event) => setDisplayName(event.target.value)} required />
          </label>
        ) : null}
        <label>
          Password
          <input type="password" value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="current-password" required />
        </label>
        {error ? <p className="error-banner">{error}</p> : null}
        <button type="submit" disabled={busy}>{busy ? 'Please wait...' : 'Continue'}</button>
      </form>
      {!isSuperAdminMode ? (
        <button className="secondary-btn" onClick={() => setMode(mode === 'login' ? 'register' : 'login')}>
          {mode === 'login' ? 'Need account link? Register here' : 'Back to login'}
        </button>
      ) : null}
    </section>
  )
}

function UserPortal({ auth, onLogout }: { auth: AuthResponse; onLogout: () => void }) {
  const [tickets, setTickets] = useState<TicketSummary[]>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [createOpen, setCreateOpen] = useState(false)
  const [activeTicketId, setActiveTicketId] = useState<string | null>(null)
  const [mode, setMode] = useState<'view' | 'update'>('view')

  const reload = async () => {
    setLoading(true)
    setError('')
    try {
      const list = await apiRequest<TicketSummary[]>('/api/tickets', undefined, auth.accessToken)
      setTickets(list)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load tickets.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void reload()
  }, [])

  const closeTicket = async (ticketId: string) => {
    await apiRequest<TicketDetail>(`/api/tickets/${ticketId}/close`, { method: 'POST' }, auth.accessToken)
    await reload()
  }

  const deleteTicket = async (ticketId: string) => {
    await apiRequest<void>(`/api/tickets/${ticketId}`, { method: 'DELETE' }, auth.accessToken)
    await reload()
  }

  return (
    <div className="plain-page">
      <header className="panel-header user-header">
        <div>
          <h2>My Tickets</h2>
          <span>{auth.displayName} ({auth.email})</span>
        </div>
        <div className="actions">
          <button onClick={() => setCreateOpen(true)}>Create New Ticket</button>
          <button className="secondary-btn" onClick={onLogout}>Logout</button>
        </div>
      </header>

      {error ? <p className="error-banner">{error}</p> : null}
      {loading ? <p>Loading tickets...</p> : null}

      <TicketStatusSummaryBand title="My Ticket Summary" tickets={tickets} />

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Ticket No.</th>
              <th>Description</th>
              <th>Date</th>
              <th>Time</th>
              <th>Assigned Agent</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {tickets.map((ticket) => {
              const created = new Date(ticket.createdUtc)
              return (
                <tr key={ticket.id}>
                  <td>{ticket.ticketNumber}</td>
                  <td>{ticket.description}</td>
                  <td>{created.toLocaleDateString()}</td>
                  <td>{created.toLocaleTimeString()}</td>
                  <td>{ticket.assignedAgentEmail ?? 'Unassigned'}</td>
                  <td>
                    <span className={`status-badge ${getStatusBadgeClass(ticket.status)}`}>
                      {toStatusLabel(ticket.status)}
                    </span>
                  </td>
                  <td className="actions">
                    <button onClick={() => { setMode('update'); setActiveTicketId(ticket.id) }}>Update Ticket</button>
                    <button onClick={() => { setMode('view'); setActiveTicketId(ticket.id) }}>View Ticket</button>
                    <button onClick={() => void deleteTicket(ticket.id)}>Delete Ticket</button>
                    <button onClick={() => void closeTicket(ticket.id)}>Close Ticket</button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {createOpen ? (
        <TicketCreateModal
          token={auth.accessToken}
          onClose={() => setCreateOpen(false)}
          onSaved={async () => {
            setCreateOpen(false)
            await reload()
          }}
        />
      ) : null}

      {activeTicketId ? (
        <TicketDetailModal
          token={auth.accessToken}
          ticketId={activeTicketId}
          mode={mode}
          onClose={() => setActiveTicketId(null)}
          onChanged={async () => {
            await reload()
          }}
        />
      ) : null}
    </div>
  )
}

function TicketCreateModal({ token, onClose, onSaved }: { token: string; onClose: () => void; onSaved: () => Promise<void> }) {
  const [requesterName, setRequesterName] = useState('')
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [message, setMessage] = useState('')
  const [category, setCategory] = useState('General')
  const [priority, setPriority] = useState('Medium')
  const [files, setFiles] = useState<FileList | null>(null)
  const [error, setError] = useState('')

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    try {
      const detail = await apiRequest<TicketDetail>('/api/tickets', {
        method: 'POST',
        body: JSON.stringify({ requesterName, title, description, message, category, priority })
      }, token)

      if (files) {
        for (const file of Array.from(files)) {
          const form = new FormData()
          form.append('file', file)
          await apiRequest<TicketDetail>(`/api/tickets/${detail.id}/attachments`, { method: 'POST', body: form, headers: {} }, token)
        }
      }

      await onSaved()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create ticket.')
    }
  }

  return (
    <Modal title="Create New Ticket" onClose={onClose}>
      <form className="ticket-form" onSubmit={submit}>
        <label>User Name<input value={requesterName} onChange={(event) => setRequesterName(event.target.value)} required /></label>
        <label>Title<input value={title} onChange={(event) => setTitle(event.target.value)} required /></label>
        <label>Description<textarea rows={3} value={description} onChange={(event) => setDescription(event.target.value)} required /></label>
        <label>Message<textarea rows={3} value={message} onChange={(event) => setMessage(event.target.value)} required /></label>
        <label>
          Category
          <select value={category} onChange={(event) => setCategory(event.target.value)} required>
            {CATEGORY_OPTIONS.map((option) => (
              <option key={option} value={option}>{option}</option>
            ))}
          </select>
        </label>
        <label>
          Priority
          <select value={priority} onChange={(event) => setPriority(event.target.value)} required>
            {PRIORITY_OPTIONS.map((option) => (
              <option key={option} value={option}>{option}</option>
            ))}
          </select>
        </label>
        <label>Attachments<input type="file" multiple onChange={(event) => setFiles(event.target.files)} /></label>
        {error ? <p className="error-banner">{error}</p> : null}
        <div className="actions"><button type="submit">Save Ticket</button></div>
      </form>
    </Modal>
  )
}

function TicketDetailModal({
  token,
  ticketId,
  mode,
  onClose,
  onChanged
}: {
  token: string
  ticketId: string
  mode: 'view' | 'update'
  onClose: () => void
  onChanged: () => Promise<void>
}) {
  const [ticket, setTicket] = useState<TicketDetail | null>(null)
  const [remark, setRemark] = useState('')
  const [files, setFiles] = useState<FileList | null>(null)
  const [error, setError] = useState('')
  const [previewItem, setPreviewItem] = useState<TicketDetail['attachments'][number] | null>(null)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [previewError, setPreviewError] = useState('')

  const closePreview = () => {
    if (previewUrl) {
      URL.revokeObjectURL(previewUrl)
    }
    setPreviewUrl(null)
    setPreviewItem(null)
    setPreviewError('')
  }

  useEffect(() => {
    const run = async () => {
      setError('')
      try {
        const detail = await apiRequest<TicketDetail>(`/api/tickets/${ticketId}`, undefined, token)
        setTicket(detail)
      } catch (err) {
        setError(err instanceof Error ? err.message : 'Unable to load ticket details.')
      }
    }
    void run()
  }, [ticketId, token])

  const submitUpdate = async (event: FormEvent) => {
    event.preventDefault()
    if (!ticket) {
      return
    }

    setError('')
    try {
      let detail = await apiRequest<TicketDetail>(`/api/tickets/${ticket.id}`, {
        method: 'PUT',
        body: JSON.stringify({
          title: ticket.title,
          description: ticket.description,
          message: ticket.message,
          category: ticket.category,
          priority: ticket.priority,
          status: ticket.status
        })
      }, token)

      if (remark.trim()) {
        detail = await apiRequest<TicketDetail>(`/api/tickets/${ticket.id}/response`, {
          method: 'POST',
          body: JSON.stringify({ message: remark.trim() })
        }, token)
      }

      if (files) {
        for (const file of Array.from(files)) {
          const form = new FormData()
          form.append('file', file)
          detail = await apiRequest<TicketDetail>(`/api/tickets/${ticket.id}/attachments`, { method: 'POST', body: form, headers: {} }, token)
        }
      }

      setTicket(detail)
      setRemark('')
      setFiles(null)
      await onChanged()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Update failed.')
    }
  }

  const openAttachmentPreview = async (attachment: TicketDetail['attachments'][number]) => {
    setPreviewError('')
    startGlobalLoader()
    try {
      const response = await fetch(`${API_BASE_URL}/api/tickets/${ticketId}/attachments/${attachment.id}/content`, {
        headers: {
          Authorization: `Bearer ${token}`
        }
      })

      if (!response.ok) {
        const text = await response.text()
        throw new Error(text || `Unable to load attachment (${response.status}).`)
      }

      const blob = await response.blob()
      const url = URL.createObjectURL(blob)
      setPreviewItem(attachment)
      setPreviewUrl(url)
    } catch (err) {
      setPreviewItem(attachment)
      setPreviewError(err instanceof Error ? err.message : 'Unable to preview attachment.')
    } finally {
      stopGlobalLoader()
    }
  }

  if (!ticket) {
    return <Modal title="Ticket" onClose={onClose}><p>Loading...</p></Modal>
  }

  return (
    <Modal title={`${mode === 'view' ? 'View' : 'Update'} ${ticket.ticketNumber}`} onClose={onClose}>
      <form className="ticket-form" onSubmit={submitUpdate}>
        <label>Title<input value={ticket.title} onChange={(event) => setTicket({ ...ticket, title: event.target.value })} readOnly={mode === 'view'} /></label>
        <label>Description<textarea rows={3} value={ticket.description} onChange={(event) => setTicket({ ...ticket, description: event.target.value })} readOnly={mode === 'view'} /></label>
        <label>Message<textarea rows={3} value={ticket.message} onChange={(event) => setTicket({ ...ticket, message: event.target.value })} readOnly={mode === 'view'} /></label>
        <label>
          Category
          <select
            value={ticket.category}
            onChange={(event) => setTicket({ ...ticket, category: event.target.value })}
            disabled={mode === 'view'}
          >
            {CATEGORY_OPTIONS.map((option) => (
              <option key={option} value={option}>{option}</option>
            ))}
          </select>
        </label>
        <label>
          Priority
          <select
            value={ticket.priority}
            onChange={(event) => setTicket({ ...ticket, priority: event.target.value })}
            disabled={mode === 'view'}
          >
            {PRIORITY_OPTIONS.map((option) => (
              <option key={option} value={option}>{option}</option>
            ))}
          </select>
        </label>
        <label>Assigned Agent<input value={ticket.assignedAgentEmail ?? 'Unassigned'} readOnly /></label>
        <label>
          Status
          <select value={ticket.status} onChange={(event) => setTicket({ ...ticket, status: event.target.value as TicketStatus })} disabled={mode === 'view'}>
            <option value="Open">Open</option>
            <option value="InProgress">In Progress</option>
            <option value="Resolved">Resolved</option>
            <option value="Closed">Closed</option>
          </select>
        </label>
        {mode === 'update' ? (
          <>
            <label>Remark<textarea rows={3} value={remark} onChange={(event) => setRemark(event.target.value)} /></label>
            <label>Add Attachments<input type="file" multiple onChange={(event) => setFiles(event.target.files)} /></label>
          </>
        ) : null}
        {error ? <p className="error-banner">{error}</p> : null}
        {mode === 'update' ? <button type="submit">Save Changes</button> : null}
      </form>
      <article className="detail-card">
        <h3>History</h3>
        <ul className="history-list">
          {ticket.history.map((item) => (
            <li key={item.id}>
              <span>{new Date(item.createdUtc).toLocaleString()}</span>
              <p>{item.description}</p>
              <small>by {item.actorEmail}</small>
            </li>
          ))}
        </ul>
      </article>
      <article className="detail-card">
        <h3>Attachments</h3>
        <ul className="history-list">
          {ticket.attachments.map((item) => (
            <li key={item.id}>
              <span>{new Date(item.uploadedUtc).toLocaleString()}</span>
              <p>{item.fileName}</p>
              <small>{item.contentType} by {item.uploadedByEmail}</small>
              <div className="actions">
                <button type="button" onClick={() => void openAttachmentPreview(item)}>View Attachment</button>
              </div>
            </li>
          ))}
        </ul>
      </article>
      {previewItem ? (
        <Modal title={`Attachment: ${previewItem.fileName}`} onClose={closePreview}>
          {previewError ? <p className="error-banner">{previewError}</p> : null}
          {previewUrl && previewItem.contentType.startsWith('image/') ? (
            <img src={previewUrl} alt={previewItem.fileName} style={{ maxWidth: '100%', maxHeight: '70vh', objectFit: 'contain' }} />
          ) : null}
          {previewUrl && previewItem.contentType === 'application/pdf' ? (
            <iframe title={previewItem.fileName} src={previewUrl} style={{ width: '100%', height: '70vh', border: 'none' }} />
          ) : null}
          {previewUrl && previewItem.contentType === 'text/plain' ? (
            <iframe title={previewItem.fileName} src={previewUrl} style={{ width: '100%', height: '70vh', border: 'none' }} />
          ) : null}
          {previewUrl && !previewItem.contentType.startsWith('image/') && previewItem.contentType !== 'application/pdf' && previewItem.contentType !== 'text/plain' ? (
            <p>Inline preview is not available for this file type.</p>
          ) : null}
          {previewUrl ? (
            <div className="actions">
              <a href={previewUrl} download={previewItem.fileName}>Download Attachment</a>
            </div>
          ) : null}
        </Modal>
      ) : null}
    </Modal>
  )
}

function SuperAdminPortal({ auth, onLogout }: { auth: AuthResponse; onLogout: () => void }) {
  const [tab, setTab] = useState<AdminTab>('applications')
  return (
    <div className="admin-shell">
      <aside className="side-menu">
        <h1>Super Admin</h1>
        <p className="subtitle">{auth.displayName}</p>
        <nav>
          <button className={tab === 'applications' ? 'menu-btn active' : 'menu-btn'} onClick={() => setTab('applications')}>Application Management</button>
          <button className={tab === 'users' ? 'menu-btn active' : 'menu-btn'} onClick={() => setTab('users')}>User Management</button>
          <button className={tab === 'tickets' ? 'menu-btn active' : 'menu-btn'} onClick={() => setTab('tickets')}>Ticket Management</button>
        </nav>
        <button className="secondary-btn" onClick={onLogout}>Logout</button>
      </aside>
      <main className="main-panel">
        {tab === 'applications' ? <ApplicationsPage token={auth.accessToken} /> : null}
        {tab === 'users' ? <UsersPage token={auth.accessToken} /> : null}
        {tab === 'tickets' ? <AdminTicketsPage token={auth.accessToken} /> : null}
      </main>
    </div>
  )
}

function ApplicationsPage({ token }: { token: string }) {
  const [rows, setRows] = useState<ApplicationAdmin[]>([])
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [error, setError] = useState('')

  const load = async () => {
    setRows(await apiRequest<ApplicationAdmin[]>('/api/admin/applications', undefined, token))
  }

  useEffect(() => {
    void load()
  }, [])

  const create = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    try {
      await apiRequest<ApplicationAdmin>('/api/admin/applications', {
        method: 'POST',
        body: JSON.stringify({ code, name })
      }, token)
      setCode('')
      setName('')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create application.')
    }
  }

  const remove = async (id: string) => {
    await apiRequest<void>(`/api/admin/applications/${id}`, { method: 'DELETE' }, token)
    await load()
  }

  return (
    <section>
      <header className="panel-header"><h2>Application Management</h2></header>
      <form className="ticket-form" onSubmit={create}>
        <label>Code<input value={code} onChange={(event) => setCode(event.target.value)} required /></label>
        <label>Name<input value={name} onChange={(event) => setName(event.target.value)} required /></label>
        {error ? <p className="error-banner">{error}</p> : null}
        <button type="submit">Add Application</button>
      </form>
      <div className="table-wrap">
        <table>
          <thead><tr><th>Code</th><th>Name</th><th>Site ID</th><th>Actions</th></tr></thead>
          <tbody>
            {rows.map((item) => (
              <tr key={item.id}>
                <td>{item.code}</td>
                <td>{item.name}</td>
                <td>{item.id}</td>
                <td><button onClick={() => void remove(item.id)}>Delete</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function UsersPage({ token }: { token: string }) {
  const [rows, setRows] = useState<UserAdmin[]>([])
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [isSuperAdmin, setIsSuperAdmin] = useState(false)
  const [appCodes, setAppCodes] = useState('')
  const [error, setError] = useState('')

  const load = async () => {
    setRows(await apiRequest<UserAdmin[]>('/api/admin/users', undefined, token))
  }

  useEffect(() => {
    void load()
  }, [])

  const create = async (event: FormEvent) => {
    event.preventDefault()
    setError('')
    try {
      await apiRequest<UserAdmin>('/api/admin/users', {
        method: 'POST',
        body: JSON.stringify({
          displayName,
          email,
          password,
          isSuperAdmin,
          appCodes: appCodes.split(',').map((x) => x.trim()).filter(Boolean)
        })
      }, token)
      setDisplayName('')
      setEmail('')
      setPassword('')
      setIsSuperAdmin(false)
      setAppCodes('')
      await load()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create user.')
    }
  }

  const remove = async (id: string) => {
    await apiRequest<void>(`/api/admin/users/${id}`, { method: 'DELETE' }, token)
    await load()
  }

  return (
    <section>
      <header className="panel-header"><h2>User Management</h2></header>
      <form className="ticket-form" onSubmit={create}>
        <label>Name<input value={displayName} onChange={(event) => setDisplayName(event.target.value)} required /></label>
        <label>Email<input type="email" value={email} onChange={(event) => setEmail(event.target.value)} required /></label>
        <label>Password<input type="text" value={password} onChange={(event) => setPassword(event.target.value)} required /></label>
        <label>Linked app codes (comma separated)<input value={appCodes} onChange={(event) => setAppCodes(event.target.value)} /></label>
        <label><input type="checkbox" checked={isSuperAdmin} onChange={(event) => setIsSuperAdmin(event.target.checked)} /> Super Admin</label>
        {error ? <p className="error-banner">{error}</p> : null}
        <button type="submit">Add User</button>
      </form>
      <div className="table-wrap">
        <table>
          <thead><tr><th>Email</th><th>Name</th><th>Role</th><th>Applications</th><th>Actions</th></tr></thead>
          <tbody>
            {rows.map((item) => (
              <tr key={item.id}>
                <td>{item.email}</td>
                <td>{item.displayName}</td>
                <td>{item.isSuperAdmin ? 'Super Admin' : 'User'}</td>
                <td>{item.appCodes.join(', ')}</td>
                <td><button onClick={() => void remove(item.id)}>Delete</button></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}

function AdminTicketsPage({ token }: { token: string }) {
  const [tickets, setTickets] = useState<TicketSummary[]>([])
  const [summaryTickets, setSummaryTickets] = useState<TicketSummary[]>([])
  const [users, setUsers] = useState<UserAdmin[]>([])
  const [applications, setApplications] = useState<ApplicationAdmin[]>([])
  const [selectedApplicationId, setSelectedApplicationId] = useState('')
  const [activeTicketId, setActiveTicketId] = useState<string | null>(null)
  const [assignTicketId, setAssignTicketId] = useState<string | null>(null)
  const [agentUserId, setAgentUserId] = useState('')
  const [assignError, setAssignError] = useState('')
  const [mode, setMode] = useState<'view' | 'update'>('view')

  const load = async () => {
    const path = selectedApplicationId
      ? `/api/tickets?${new URLSearchParams({ applicationId: selectedApplicationId }).toString()}`
      : '/api/tickets'

    const scopedTickets = await apiRequest<TicketSummary[]>(path, undefined, token)
    setSummaryTickets(scopedTickets)

    if (selectedApplicationId) {
      setTickets(scopedTickets)
      return
    }

    setTickets([])
  }

  const loadUsers = async () => {
    setUsers(await apiRequest<UserAdmin[]>('/api/admin/users', undefined, token))
  }

  const loadApplications = async () => {
    setApplications(await apiRequest<ApplicationAdmin[]>('/api/admin/applications', undefined, token))
  }

  useEffect(() => {
    void loadUsers()
    void loadApplications()
  }, [])

  useEffect(() => {
    void load()
  }, [selectedApplicationId])

  const closeTicket = async (ticketId: string) => {
    await apiRequest<TicketDetail>(`/api/tickets/${ticketId}/close`, { method: 'POST' }, token)
    await load()
  }

  const deleteTicket = async (ticketId: string) => {
    await apiRequest<void>(`/api/tickets/${ticketId}`, { method: 'DELETE' }, token)
    await load()
  }

  const assignAgent = async () => {
    if (!assignTicketId || !agentUserId) {
      setAssignError('Please select an agent.')
      return
    }

    setAssignError('')
    try {
      await apiRequest<TicketDetail>(`/api/tickets/${assignTicketId}/assign`, {
        method: 'POST',
        body: JSON.stringify({ agentUserId })
      }, token)
      setAssignTicketId(null)
      setAgentUserId('')
      await load()
    } catch (err) {
      setAssignError(err instanceof Error ? err.message : 'Failed to assign agent.')
    }
  }

  return (
    <section>
      <header className="panel-header"><h2>Ticket Management</h2></header>
      <div className="ticket-form" style={{ marginBottom: '1rem' }}>
        <label>
          Application
          <select value={selectedApplicationId} onChange={(event) => setSelectedApplicationId(event.target.value)}>
            <option value="">Select application</option>
            {applications.map((application) => (
              <option key={application.id} value={application.id}>{application.name}</option>
            ))}
          </select>
        </label>
      </div>

      <TicketStatusSummaryBand
        title={selectedApplicationId ? 'Selected Application Ticket Summary' : 'All Applications Ticket Summary'}
        tickets={summaryTickets}
      />

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Ticket No.</th>
              <th>Description</th>
              <th>Date</th>
              <th>Time</th>
              <th>Assigned Agent</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {!selectedApplicationId ? (
              <tr>
                <td colSpan={7}>Select an application to view tickets.</td>
              </tr>
            ) : null}
            {tickets.map((ticket) => {
              const created = new Date(ticket.createdUtc)
              return (
                <tr key={ticket.id}>
                  <td>{ticket.ticketNumber}</td>
                  <td>{ticket.description}</td>
                  <td>{created.toLocaleDateString()}</td>
                  <td>{created.toLocaleTimeString()}</td>
                  <td>{ticket.assignedAgentEmail ?? 'Unassigned'}</td>
                  <td>
                    <span className={`status-badge ${getStatusBadgeClass(ticket.status)}`}>
                      {toStatusLabel(ticket.status)}
                    </span>
                  </td>
                  <td className="actions">
                    <button onClick={() => { setAssignTicketId(ticket.id); setAgentUserId(''); setAssignError('') }}>Assign Agent</button>
                    <button onClick={() => { setMode('update'); setActiveTicketId(ticket.id) }}>Update Ticket</button>
                    <button onClick={() => { setMode('view'); setActiveTicketId(ticket.id) }}>View Ticket</button>
                    <button onClick={() => void deleteTicket(ticket.id)}>Delete Ticket</button>
                    <button onClick={() => void closeTicket(ticket.id)}>Close Ticket</button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      {assignTicketId ? (
        <Modal title="Assign Agent" onClose={() => setAssignTicketId(null)}>
          <div className="ticket-form">
            <label>
              Select Agent
              <select value={agentUserId} onChange={(event) => setAgentUserId(event.target.value)}>
                <option value="">Select a user</option>
                {users.map((user) => (
                  <option key={user.id} value={user.id}>{user.displayName} ({user.email})</option>
                ))}
              </select>
            </label>
            {assignError ? <p className="error-banner">{assignError}</p> : null}
            <div className="actions">
              <button onClick={() => void assignAgent()}>Assign</button>
            </div>
          </div>
        </Modal>
      ) : null}
      {activeTicketId ? (
        <TicketDetailModal
          token={token}
          ticketId={activeTicketId}
          mode={mode}
          onClose={() => setActiveTicketId(null)}
          onChanged={load}
        />
      ) : null}
    </section>
  )
}

function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <section className="modal-card">
        <header className="panel-header">
          <h2>{title}</h2>
          <button className="secondary-btn" onClick={onClose}>Close</button>
        </header>
        {children}
      </section>
    </div>
  )
}

export default App
