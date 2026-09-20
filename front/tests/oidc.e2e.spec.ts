import { createServer } from 'node:http'
import type { Server } from 'node:http'
import { test, expect } from '@playwright/test'

let provider: Server
let authorization: URL | undefined
let tokenCalls = 0

test.beforeAll(async () => {
  provider = createServer((request, response) => {
    const url = new URL(request.url ?? '/', 'http://127.0.0.1:4179')
    response.setHeader('Access-Control-Allow-Origin', 'http://127.0.0.1:5174')
    if (url.pathname === '/realms/test/.well-known/openid-configuration') {
      response.setHeader('Content-Type', 'application/json')
      response.end(JSON.stringify({ authorization_endpoint: 'http://127.0.0.1:4179/authorize', token_endpoint: 'http://127.0.0.1:4179/token' }))
      return
    }
    if (url.pathname === '/authorize') {
      authorization = url
      const redirectUri = url.searchParams.get('redirect_uri')!
      const callback = new URL(redirectUri)
      callback.searchParams.set('code', 'approved-code')
      callback.searchParams.set('state', url.searchParams.get('state')!)
      response.writeHead(302, { Location: callback.toString() })
      response.end()
      return
    }
    if (url.pathname === '/token' && request.method === 'POST') {
      tokenCalls++
      response.setHeader('Content-Type', 'application/json')
      response.end(JSON.stringify({ access_token: 'controlled-access-token', expires_in: 3600, token_type: 'Bearer' }))
      return
    }
    response.statusCode = 404
    response.end()
  })
  await new Promise<void>((resolve) => provider.listen(4179, '127.0.0.1', resolve))
})

test.afterAll(async () => {
  await new Promise<void>((resolve, reject) => provider.close((error) => error ? reject(error) : resolve()))
})

test('redirects a protected route through Authorization Code + PKCE and uses the in-memory Bearer token', async ({ page }) => {
  const apiHeaders: string[] = []
  await page.route('http://127.0.0.1:5080/**', async (route) => {
    apiHeaders.push(route.request().headers().authorization ?? '')
    await route.fulfill({ json: [] })
  })
  await page.route('http://127.0.0.1:5081/**', async (route) => {
    apiHeaders.push(route.request().headers().authorization ?? '')
    await route.fulfill({ json: { credits: 0, debits: 0, balance: 0, freshnessStatus: 'current' } })
  })

  await page.goto('/')
  await expect(page.getByRole('heading', { name: /Seu dinheiro/i })).toBeVisible()
  await expect(page.getByText('Você ainda não tem lançamentos.')).toBeVisible()
  expect(page.url()).toBe('http://127.0.0.1:5174/')
  expect(authorization?.searchParams.get('response_type')).toBe('code')
  expect(authorization?.searchParams.get('client_id')).toBe('cashflow-front')
  expect(authorization?.searchParams.get('code_challenge_method')).toBe('S256')
  expect(authorization?.searchParams.get('code_challenge')).toMatch(/^[A-Za-z0-9_-]{43}$/)
  expect(tokenCalls).toBe(1)
  expect(apiHeaders).toEqual(['Bearer controlled-access-token', 'Bearer controlled-access-token'])
  await expect.poll(() => page.evaluate(() => ({ local: { ...localStorage }, session: { ...sessionStorage } }))).toEqual({ local: {}, session: {} })
})

test('renders a callback error without calling a business API', async ({ page }) => {
  let businessRequests = 0
  await page.route('http://127.0.0.1:5080/**', async (route) => { businessRequests++; await route.abort() })
  await page.route('http://127.0.0.1:5081/**', async (route) => { businessRequests++; await route.abort() })

  await page.goto('/auth/callback?error=access_denied&error_description=Login%20cancelado')

  await expect(page.getByRole('alert')).toContainText('Login cancelado')
  expect(businessRequests).toBe(0)
})
