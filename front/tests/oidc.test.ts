import assert from 'node:assert/strict'
import { createHash, webcrypto } from 'node:crypto'
import test, { beforeEach } from 'node:test'
import { OidcClient, activeSession, authorizedHeaders, clearSession } from '../src/features/auth/oidc.ts'

Object.defineProperty(globalThis, 'crypto', { value: webcrypto, configurable: true })

class MemoryStorage {
  private readonly values = new Map<string, string>()
  getItem(key: string) { return this.values.get(key) ?? null }
  setItem(key: string, value: string) { this.values.set(key, value) }
  removeItem(key: string) { this.values.delete(key) }
  snapshot() { return Object.fromEntries(this.values) }
}

const config = {
  authority: 'https://issuer.example/realms/cashflow',
  clientId: 'cashflow-front',
  redirectUri: 'https://app.example/auth/callback',
  scope: 'openid profile',
}

beforeEach(() => clearSession())

test('starts Authorization Code login with a S256 PKCE challenge and state', async () => {
  const storage = new MemoryStorage()
  let redirected = ''
  const client = new OidcClient(config, async () => new Response(JSON.stringify({ authorization_endpoint: 'https://issuer.example/authorize', token_endpoint: 'https://issuer.example/token' })), {
    origin: 'https://app.example', href: 'https://app.example/', assign: (url: string) => { redirected = url },
  }, storage)

  await client.beginLogin()

  const url = new URL(redirected)
  const pending = JSON.parse(storage.getItem('cashflow.oidc.pending')!) as { state: string, verifier: string }
  assert.equal(url.origin + url.pathname, 'https://issuer.example/authorize')
  assert.equal(url.searchParams.get('response_type'), 'code')
  assert.equal(url.searchParams.get('client_id'), 'cashflow-front')
  assert.equal(url.searchParams.get('redirect_uri'), config.redirectUri)
  assert.equal(url.searchParams.get('scope'), 'openid profile')
  assert.equal(url.searchParams.get('state'), pending.state)
  assert.equal(url.searchParams.get('code_challenge_method'), 'S256')
  const expectedChallenge = createHash('sha256').update(pending.verifier).digest('base64url')
  assert.equal(url.searchParams.get('code_challenge'), expectedChallenge)
})

test('exchanges a valid callback code, keeps only the access token in memory, and builds Bearer headers', async () => {
  const storage = new MemoryStorage()
  let tokenRequest: RequestInit | undefined
  const location = { origin: 'https://app.example', href: 'https://app.example/auth/callback', assign: () => undefined }
  const client = new OidcClient(config, async (url, init) => {
    if (String(url).endsWith('openid-configuration')) return new Response(JSON.stringify({ authorization_endpoint: 'https://issuer.example/authorize', token_endpoint: 'https://issuer.example/token' }))
    tokenRequest = init
    return new Response(JSON.stringify({ access_token: 'an-access-token', expires_in: 3600 }))
  }, location, storage)
  await client.beginLogin()
  const state = JSON.parse(storage.getItem('cashflow.oidc.pending')!).state

  const established = await client.completeCallback(`https://app.example/auth/callback?code=approved-code&state=${state}`)

  assert.equal(established.accessToken, 'an-access-token')
  assert.equal(activeSession()?.accessToken, 'an-access-token')
  assert.deepEqual(storage.snapshot(), {})
  assert.equal(new URLSearchParams(String(tokenRequest?.body)).get('grant_type'), 'authorization_code')
  assert.equal(new URLSearchParams(String(tokenRequest?.body)).get('code'), 'approved-code')
  assert.equal(new URLSearchParams(String(tokenRequest?.body)).get('client_id'), 'cashflow-front')
  assert.match(String(tokenRequest?.body), /code_verifier=/)
  assert.deepEqual(authorizedHeaders(established.accessToken, { 'X-Correlation-Id': 'test' }), { Accept: 'application/json', 'X-Correlation-Id': 'test', Authorization: 'Bearer an-access-token' })
})

test('rejects OIDC callback errors and state mismatches without calling the token endpoint', async () => {
  const storage = new MemoryStorage()
  let tokenCalls = 0
  const client = new OidcClient(config, async () => {
    tokenCalls++
    return new Response(JSON.stringify({ authorization_endpoint: 'https://issuer.example/authorize', token_endpoint: 'https://issuer.example/token' }))
  }, { origin: 'https://app.example', href: 'https://app.example/', assign: () => undefined }, storage)

  await assert.rejects(client.completeCallback('https://app.example/auth/callback?error=access_denied&error_description=cancelled'), /cancelled/)
  assert.equal(tokenCalls, 0)
  storage.setItem('cashflow.oidc.pending', JSON.stringify({ state: 'expected', verifier: 'verifier' }))
  await assert.rejects(client.completeCallback('https://app.example/auth/callback?code=code&state=other'), /Resposta de login inválida/)
  assert.equal(tokenCalls, 0)
  assert.deepEqual(storage.snapshot(), {})
})
