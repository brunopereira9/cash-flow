import { useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { activeSession, browserOidcClient, clearSession, defaultOidcConfiguration } from './features/auth/oidc'
import { StatusBanner } from './components/StatusBanner'
import { ApiError } from './lib/http'
import { createLedgerEntry, listLedgerEntries } from './features/ledger/api'
import { getDailySummary } from './features/summary/api'
import { listAuditRecords } from './features/audit/api'
import { listUsers, updateUser } from './features/users/api'
import type { LedgerEntry } from './features/ledger/types'
import type { DailySummary } from './features/summary/types'
import type { AuditRecord } from './features/audit/types'
import type { UserSummary } from './features/users/types'
import './App.css'

type Entry = LedgerEntry
type Summary = DailySummary
const currentFreshness = 'current'
type ScreenState = 'loading' | 'ready' | 'empty' | 'error' | 'unauthorized'
type AuthState = { kind: 'loading' } | { kind: 'error', message: string } | { kind: 'authenticated', accessToken: string, role?: 'admin' | 'operator' | 'auditor' }
type View = 'summary' | 'audit' | 'users'

const currency = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

function App() {
  const [auth, setAuth] = useState<AuthState>({ kind: 'loading' })
  const [entries, setEntries] = useState<Entry[]>([])
  const [summary, setSummary] = useState<Summary | null>(null)
  const [screenState, setScreenState] = useState<ScreenState>('loading')
  const [formOpen, setFormOpen] = useState(false)
  const [saving, setSaving] = useState(false)
  const [saved, setSaved] = useState(false)
  const [view, setView] = useState<View>('summary')
  const [auditRecords, setAuditRecords] = useState<AuditRecord[]>([])
  const [users, setUsers] = useState<UserSummary[]>([])
  const [panelState, setPanelState] = useState<'idle' | 'loading' | 'ready' | 'empty' | 'error'>('idle')
  const [auditFilter, setAuditFilter] = useState('')
  const [editingUser, setEditingUser] = useState<UserSummary | null>(null)
  const [userMessage, setUserMessage] = useState<string | null>(null)
  const client = useMemo(() => browserOidcClient(), [])
  const started = useRef(false)

  useEffect(() => {
    if (started.current) return
    started.current = true
    const callbackPath = new URL(defaultOidcConfiguration(window.location).redirectUri).pathname
    if (window.location.pathname === callbackPath) {
      void client.completeCallback()
        .then((value) => {
          window.history.replaceState({}, '', '/')
          setAuth({ kind: 'authenticated', accessToken: value.accessToken, role: value.role })
        })
        .catch((reason: unknown) => setAuth({ kind: 'error', message: reason instanceof Error ? reason.message : 'Não foi possível concluir o login.' }))
      return
    }
    const current = activeSession()
    if (current && current.expiresAt > Date.now()) {
      queueMicrotask(() => setAuth({ kind: 'authenticated', accessToken: current.accessToken, role: current.role }))
      return
    }
    void client.beginLogin().catch((reason: unknown) => setAuth({ kind: 'error', message: reason instanceof Error ? reason.message : 'Não foi possível iniciar o login.' }))
  }, [client])

  useEffect(() => {
    if (auth.kind !== 'authenticated') return
    Promise.all([
      listLedgerEntries(auth.accessToken),
      getDailySummary(auth.accessToken, new Date().toISOString().slice(0, 10)),
    ])
      .then(([items, dailySummary]) => {
        setEntries(items)
        setSummary(dailySummary)
        setScreenState(items.length === 0 ? 'empty' : 'ready')
      })
      .catch((reason: unknown) => setScreenState(reason instanceof ApiError && [401, 403].includes(reason.status) ? 'unauthorized' : 'error'))
  }, [auth])

  function retryLogin() {
    clearSession()
    setAuth({ kind: 'loading' })
    void client.beginLogin().catch((reason: unknown) => setAuth({ kind: 'error', message: reason instanceof Error ? reason.message : 'Não foi possível iniciar o login.' }))
  }

  async function openView(nextView: View) {
    setView(nextView)
    if (nextView === 'summary' || auth.kind !== 'authenticated') return
    setPanelState('loading')
    try {
      if (nextView === 'audit') setAuditRecords(await listAuditRecords(auth.accessToken, auditFilter ? { actor: auditFilter } : {}))
      if (nextView === 'users') setUsers(await listUsers(auth.accessToken))
      setPanelState('ready')
    } catch {
      setPanelState('error')
    }
  }

  async function saveUser(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (auth.kind !== 'authenticated' || !editingUser) return
    const data = new FormData(event.currentTarget)
    const next = { ...editingUser, email: String(data.get('email') || ''), role: String(data.get('role')) as UserSummary['role'], active: data.get('active') === 'on' }
    try {
      await updateUser(auth.accessToken, editingUser.id, next)
      setUsers((current) => current.map((user) => user.id === next.id ? next : user))
      setEditingUser(null)
      setUserMessage('Usuário atualizado.')
    } catch (error) {
      setUserMessage(error instanceof Error ? error.message : 'Não foi possível atualizar o usuário.')
    }
  }

  async function createEntry(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (auth.kind !== 'authenticated') return
    setSaving(true)
    setSaved(false)
    const form = event.currentTarget
    const values = new FormData(form)
    try {
      const entry = await createLedgerEntry(auth.accessToken, { amount: Number(values.get('amount')), type: String(values.get('type')), description: String(values.get('description')) })
      setEntries((current) => [entry, ...current])
      setSaved(true)
      form.reset()
    } catch (reason: unknown) {
      setScreenState(reason instanceof ApiError && [401, 403].includes(reason.status) ? 'unauthorized' : 'error')
    }
    setSaving(false)
  }

  if (auth.kind === 'loading') return <main className="dashboard"><p className="state-copy" aria-live="polite">Encaminhando para o login seguro…</p></main>
  if (auth.kind === 'error') return <main className="dashboard"><div role="alert" className="error-state">Não foi possível concluir o login: {auth.message}</div><button className="primary" onClick={retryLogin}>Tentar novamente</button></main>

  const freshness = summary?.freshnessStatus
  const canAudit = auth.role === 'admin' || auth.role === 'auditor'
  const canManageUsers = auth.role === 'admin'
  const balanceLabel = screenState === 'loading' ? 'Carregando consolidado…'
    : screenState === 'unauthorized' ? 'Acesso não autorizado'
      : screenState === 'error' || freshness === 'unavailable' ? 'Indisponível'
        : freshness === 'stale' ? 'Dados em atualização · stale'
          : `Atualizado agora · ${currentFreshness}`

  return <main className="dashboard">
    <nav className="topbar" aria-label="Navegação principal"><span className="wordmark"><span className="dot" /> fluxo</span><div className="nav-links"><button className={view === 'summary' ? 'nav-link active' : 'nav-link'} onClick={() => void openView('summary')}>Visão geral</button><button className={view === 'audit' ? 'nav-link active' : 'nav-link'} onClick={() => void openView('audit')} hidden={!canAudit}>Auditoria</button><button className={view === 'users' ? 'nav-link active' : 'nav-link'} onClick={() => void openView('users')} hidden={!canManageUsers}>Usuários</button></div><span className="role-label">{auth.role || 'conta'}</span><button className="avatar" aria-label="Conta demo">{auth.role === 'admin' ? 'AD' : auth.role === 'auditor' ? 'AU' : 'OP'}</button></nav>
    {screenState === 'error' && <StatusBanner tone="error">Não foi possível atualizar os dados. Tente novamente.</StatusBanner>}
    {screenState === 'unauthorized' && <StatusBanner tone="error">Acesso não autorizado. Entre novamente para consultar os dados.</StatusBanner>}
    {view === 'audit' && <section className="activity feature-panel" aria-label="Auditoria"><div className="section-heading"><div><span className="eyebrow">AUDITORIA</span><h2>Histórico de alterações</h2></div><form onSubmit={(event) => { event.preventDefault(); void openView('audit') }}><input aria-label="Filtrar por ator" placeholder="Filtrar por ator" value={auditFilter} onChange={(event) => setAuditFilter(event.target.value)} /><button className="secondary">Filtrar</button></form></div>{panelState === 'loading' && <p className="state-copy">Carregando auditoria…</p>}{panelState === 'error' && <StatusBanner tone="error">Não foi possível consultar a auditoria.</StatusBanner>}{panelState === 'ready' && auditRecords.length === 0 && <p className="state-copy">Nenhum registro encontrado.</p>}{auditRecords.map((record) => <div className="transaction" key={record.id}><div><strong>{record.operation}</strong><small>{record.occurredAt} · {record.actorId} · {record.correlationId}</small></div><span>{record.before ? 'antes/depois disponível' : 'sem dados sensíveis'}</span></div>)}</section>}
    {view === 'users' && <section className="activity feature-panel" aria-label="Usuários"><div className="section-heading"><div><span className="eyebrow">IDENTIDADE</span><h2>Usuários e papéis</h2></div></div>{userMessage && <StatusBanner tone="success">{userMessage}</StatusBanner>}{panelState === 'loading' && <p className="state-copy">Carregando usuários…</p>}{panelState === 'error' && <StatusBanner tone="error">Acesso restrito ou serviço indisponível.</StatusBanner>}{panelState === 'ready' && users.length === 0 && <p className="state-copy">Nenhum usuário encontrado.</p>}{users.map((user) => <div className="transaction" key={user.id}><div><strong>{user.username}</strong><small>{user.email || 'sem e-mail'}</small></div><b>{user.role} · {user.active ? 'ativo' : 'inativo'}</b><button className="secondary" onClick={() => { setUserMessage(null); setEditingUser(user) }}>Editar</button></div>)}{editingUser && <form className="entry-form" onSubmit={saveUser}><h3>Editar {editingUser.username}</h3><label>E-mail<input name="email" type="email" defaultValue={editingUser.email || ''} /></label><label>Papel<select name="role" defaultValue={editingUser.role}><option value="admin">Admin</option><option value="operator">Operator</option><option value="auditor">Auditor</option></select></label><label><input name="active" type="checkbox" defaultChecked={editingUser.active} /> Ativo</label><div className="form-actions"><button type="button" className="secondary" onClick={() => setEditingUser(null)}>Cancelar</button><button className="primary">Salvar</button></div></form>}</section>}
    {view === 'summary' && <section className="hero">
      <div><span className="eyebrow">CONTA OPERACIONAL</span><h1>Seu dinheiro,<br /><em>em movimento.</em></h1><p>Acompanhe entradas, saídas e o saldo do seu negócio em um só lugar.</p><button className="primary" onClick={() => setFormOpen(true)} disabled={screenState === 'unauthorized'}>+ Novo lançamento</button></div>
      <div className="balance-card" aria-live="polite"><span>Saldo disponível</span><strong>{summary ? currency.format(summary.balance) : '—'}</strong><small className={`freshness ${freshness || screenState}`}>{balanceLabel}</small><div className="sparkline" aria-hidden="true">╱╲╱╲╱╲╱╲╱╲╱</div></div>
    </section>}
    {view === 'summary' && <><section className="summary-grid" aria-label="Resumo mensal"><article><span>Entradas no mês</span><strong className="positive">{summary ? currency.format(summary.credits) : '—'}</strong><small>Consolidado diário</small></article><article><span>Saídas no mês</span><strong>{summary ? currency.format(summary.debits) : '—'}</strong><small>Consolidado diário</small></article><article><span>Transações</span><strong>{screenState === 'loading' ? '—' : entries.length}</strong><small>{screenState === 'loading' ? 'carregando' : 'no período'}</small></article></section>
    <section className="activity"><div className="section-heading"><div><span className="eyebrow">ATIVIDADE RECENTE</span><h2>Últimos lançamentos</h2></div><button className="secondary">Ver todos →</button></div>{screenState === 'loading' && <p className="state-copy">Carregando lançamentos…</p>}{screenState === 'empty' && <p className="state-copy">Você ainda não tem lançamentos.</p>}{entries.slice(0, 5).map((entry) => <div className="transaction" key={entry.id}><span className={`transaction-icon ${entry.type === 'credit' ? 'in' : 'out'}`}>{entry.type === 'credit' ? '↙' : '↗'}</span><div><strong>{entry.description}</strong><small>{entry.businessDate} · {entry.type}</small></div><b className={entry.type === 'credit' ? 'positive' : ''}>{entry.type === 'credit' ? '+' : '−'} {currency.format(entry.amount)}</b></div>)}</section></>}
    {formOpen && <div className="modal-backdrop"><form className="entry-form" onSubmit={createEntry} aria-label="Novo lançamento"><h2>Novo lançamento</h2><label>Valor<input name="amount" type="number" step="0.01" min="0.01" required /></label><label>Tipo<select name="type" defaultValue="credit"><option value="credit">Crédito</option><option value="debit">Débito</option></select></label><label>Descrição<input name="description" required /></label><label>Data<input name="businessDate" type="date" defaultValue={new Date().toISOString().slice(0, 10)} /></label><div className="form-actions"><button type="button" className="secondary" onClick={() => setFormOpen(false)}>Cancelar</button><button className="primary" disabled={saving}>{saving ? 'Salvando…' : 'Salvar lançamento'}</button></div>{saved && <StatusBanner tone="success">Lançamento salvo.</StatusBanner>}</form></div>}
  </main>
}

export default App
