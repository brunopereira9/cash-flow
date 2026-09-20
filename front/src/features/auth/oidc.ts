export type OidcConfiguration = {
  authority: string
  clientId: string
  redirectUri: string
  scope: string
}

export type AuthSession = {
  accessToken: string
  expiresAt: number
  role?: 'admin' | 'operator' | 'auditor'
}

type PendingAuthorization = {
  state: string
  verifier: string
}

type OidcMetadata = {
  authorization_endpoint: string
  token_endpoint: string
  end_session_endpoint?: string
}

type LocationLike = Pick<Location, 'assign' | 'href' | 'origin'>
type StorageLike = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>
type FetchLike = typeof fetch

export const pendingAuthorizationKey = 'cashflow.oidc.pending'
let session: AuthSession | null = null

function base64Url(bytes: Uint8Array) {
  let value = ''
  for (const byte of bytes) value += String.fromCharCode(byte)
  return btoa(value).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '')
}

function randomValue() {
  const bytes = new Uint8Array(32)
  crypto.getRandomValues(bytes)
  return base64Url(bytes)
}

async function s256(value: string) {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(value))
  return base64Url(new Uint8Array(digest))
}

function roleFromAccessToken(accessToken: string): AuthSession['role'] {
  try {
    const payload = accessToken.split('.')[1]
    if (!payload) return undefined
    const normalized = payload.replaceAll('-', '+').replaceAll('_', '/')
    const padded = normalized + '='.repeat((4 - normalized.length % 4) % 4)
    const claims = JSON.parse(atob(padded)) as {
      realm_access?: { roles?: string[] }
      roles?: string[]
    }
    const roles = claims.realm_access?.roles ?? claims.roles ?? []
    return roles.find((role): role is NonNullable<AuthSession['role']> => ['admin', 'operator', 'auditor'].includes(role))
  } catch {
    return undefined
  }
}

export function defaultOidcConfiguration(location: Pick<Location, 'origin'>): OidcConfiguration {
  return {
    // The browser reaches Keycloak through the host-published Compose port.
    authority: import.meta.env.VITE_OIDC_AUTHORITY || 'http://localhost:18081/realms/cashflow',
    clientId: import.meta.env.VITE_OIDC_CLIENT_ID || 'cashflow-front',
    redirectUri: import.meta.env.VITE_OIDC_REDIRECT_URI || `${location.origin}/auth/callback`,
    scope: import.meta.env.VITE_OIDC_SCOPE || 'openid profile',
  }
}

export function activeSession() {
  return session
}

export function clearSession() {
  session = null
}

export function authorizedHeaders(accessToken: string, headers: HeadersInit = {}) {
  return {
    Accept: 'application/json',
    ...headers,
    Authorization: `Bearer ${accessToken}`,
  }
}

export class OidcClient {
  private readonly config: OidcConfiguration
  private readonly fetcher: FetchLike
  private readonly location: LocationLike
  private readonly storage: StorageLike

  constructor(
    config: OidcConfiguration,
    fetcher: FetchLike,
    location: LocationLike,
    storage: StorageLike,
  ) {
    this.config = config
    this.fetcher = fetcher
    this.location = location
    this.storage = storage
  }

  private async metadata(): Promise<OidcMetadata> {
    const response = await this.fetcher(`${this.config.authority}/.well-known/openid-configuration`)
    if (!response.ok) throw new Error('Não foi possível iniciar o login.')
    const metadata = await response.json() as Partial<OidcMetadata>
    if (!metadata.authorization_endpoint || !metadata.token_endpoint) throw new Error('Provedor OIDC inválido.')
    return metadata as OidcMetadata
  }

  async beginLogin() {
    const pending: PendingAuthorization = { state: randomValue(), verifier: randomValue() }
    const metadata = await this.metadata()
    this.storage.setItem(pendingAuthorizationKey, JSON.stringify(pending))
    const url = new URL(metadata.authorization_endpoint)
    url.search = new URLSearchParams({
      client_id: this.config.clientId,
      code_challenge: await s256(pending.verifier),
      code_challenge_method: 'S256',
      redirect_uri: this.config.redirectUri,
      response_type: 'code',
      scope: this.config.scope,
      state: pending.state,
    }).toString()
    this.location.assign(url.toString())
  }

  async completeCallback(url = this.location.href): Promise<AuthSession> {
    const callback = new URL(url)
    const providerError = callback.searchParams.get('error')
    if (providerError) throw new Error(callback.searchParams.get('error_description') || `Login recusado: ${providerError}`)

    const code = callback.searchParams.get('code')
    const receivedState = callback.searchParams.get('state')
    const stored = this.storage.getItem(pendingAuthorizationKey)
    this.storage.removeItem(pendingAuthorizationKey)
    if (!code || !receivedState || !stored) throw new Error('Resposta de login inválida.')

    let pending: PendingAuthorization
    try {
      pending = JSON.parse(stored) as PendingAuthorization
    } catch {
      throw new Error('Resposta de login inválida.')
    }
    if (!pending.state || !pending.verifier || pending.state !== receivedState) throw new Error('Resposta de login inválida.')

    const metadata = await this.metadata()
    const response = await this.fetcher(metadata.token_endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded', Accept: 'application/json' },
      body: new URLSearchParams({
        grant_type: 'authorization_code',
        client_id: this.config.clientId,
        code,
        code_verifier: pending.verifier,
        redirect_uri: this.config.redirectUri,
      }),
    })
    if (!response.ok) throw new Error('Não foi possível concluir o login.')

    const token = await response.json() as { access_token?: string, expires_in?: number }
    if (!token.access_token || token.expires_in !== 3600) throw new Error('Token OIDC inválido.')
    session = {
      accessToken: token.access_token,
      expiresAt: Date.now() + token.expires_in * 1000,
      role: roleFromAccessToken(token.access_token),
    }
    return session
  }
}

export function browserOidcClient() {
  return new OidcClient(defaultOidcConfiguration(window.location), window.fetch.bind(window), window.location, window.sessionStorage)
}
