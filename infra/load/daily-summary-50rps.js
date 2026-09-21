import http from 'k6/http'
import { check, fail, sleep } from 'k6'
import { Rate, Trend } from 'k6/metrics'

const summaryUrl = __ENV.SUMMARY_URL || 'http://summary:8080'
const coreUrl = __ENV.CORE_URL || 'http://core:8080'
const keycloakUrl = __ENV.KEYCLOAK_URL || 'http://keycloak:8080'
const businessDate = __ENV.SUMMARY_DATE || '2026-09-19'
const entryCount = Number(__ENV.SEED_ENTRY_COUNT || 1000)
const failures = new Rate('failed_summary_responses')
const latency = new Trend('summary_latency')

export const options = {
  scenarios: {
    steady: {
      executor: 'constant-arrival-rate',
      rate: 50,
      timeUnit: '1s',
      duration: __ENV.DURATION || '5m',
      preAllocatedVUs: 20,
      maxVUs: 100,
    },
  },
  thresholds: {
    failed_summary_responses: ['rate<=0.05'],
    summary_latency: ['p(95)<500'],
  },
}

function guid(runId, index, kind) {
  const suffix = index.toString(16).padStart(12, '0')
  return `${runId.slice(0, 8)}-${runId.slice(8, 12)}-4${kind}00-8${kind}00-${suffix}`
}

function summaryRequest(authorization) {
  return http.get(`${summaryUrl}/summary/daily/${businessDate}`, {
    headers: { Authorization: authorization },
  })
}

function createEntry(authorization, runId, index, amount, type) {
  return http.post(
    `${coreUrl}/ledger/entries`,
    JSON.stringify({
      amount,
      type,
      description: `FC12 concentrated seed ${index + 1}`,
      businessDate,
    }),
    {
      headers: {
        Authorization: authorization,
        'Content-Type': 'application/json',
        'Idempotency-Key': guid(runId, index, '1'),
      },
    },
  )
}

function requireCurrentSummary(response, label) {
  const valid = check(response, {
    [`${label} returns 200`]: (result) => result.status === 200,
    [`${label} is current`]: (result) => result.json('freshnessStatus') === 'current',
  })
  if (!valid) fail(`${label} could not read a current daily summary: ${response.status} ${response.body}`)
  return response.json()
}

function waitForSeededSummary(authorization, before, expected) {
  for (let attempt = 0; attempt < 60; attempt += 1) {
    const current = requireCurrentSummary(summaryRequest(authorization), 'seeded summary')
    if (current.credits - before.credits === expected.credits &&
        current.debits - before.debits === expected.debits &&
        current.balance - before.balance === expected.balance) {
      return current
    }
    sleep(1)
  }
  fail('seeded summary did not converge within 60 seconds')
}

function accessToken() {
  for (let attempt = 0; attempt < 30; attempt += 1) {
    const response = http.post(
      `${keycloakUrl}/realms/cashflow/protocol/openid-connect/token`,
      'grant_type=password&client_id=cashflow-front&username=demo-operator&password=username123',
      { headers: { 'Content-Type': 'application/x-www-form-urlencoded' } },
    )
    if (response.status === 200 && response.json('access_token')) {
      check(response, { 'load token is issued': (result) => result.status === 200 && result.json('access_token') })
      return `Bearer ${response.json('access_token')}`
    }
    sleep(2)
  }
  fail('could not obtain the load-test token after waiting for Keycloak')
}

export function setup() {
  if (!Number.isInteger(entryCount) || entryCount < 2) fail('SEED_ENTRY_COUNT must be an integer of at least 2')

  const authorization = accessToken()
  const baselineResponse = summaryRequest(authorization)
  if (!check(baselineResponse, { 'baseline summary responds': (result) => result.status === 200 })) fail(`baseline summary failed: ${baselineResponse.status} ${baselineResponse.body}`)
  const before = baselineResponse.json()
  const runId = `${Date.now().toString(16).padStart(12, '0')}${Math.floor(Math.random() * 0xffff).toString(16).padStart(4, '0')}`
  const expected = { credits: 0, debits: 0, balance: 0 }

  for (let index = 0; index < entryCount; index += 1) {
    const credit = index % 2 === 0
    const amount = credit ? 100 : 50
    expected.credits += credit ? amount : 0
    expected.debits += credit ? 0 : amount
    expected.balance += credit ? amount : -amount
    const response = createEntry(authorization, runId, index, amount, credit ? 'credit' : 'debit')
    if (!check(response, { 'concentrated seed entry is created': (result) => result.status === 201 })) {
      fail(`seed entry ${index + 1} was not created: ${response.status} ${response.body}`)
    }
  }

  const after = waitForSeededSummary(authorization, before, expected)
  check(null, {
    'concentrated seed increases credits': () => after.credits - before.credits === expected.credits,
    'concentrated seed increases debits': () => after.debits - before.debits === expected.debits,
    'concentrated seed increases balance': () => after.balance - before.balance === expected.balance,
  })

  console.log(`FC12 seeded ${entryCount} entries on ${businessDate}: +${expected.credits} credits, +${expected.debits} debits, +${expected.balance} balance.`)
  return { authorization }
}

export default function (data) {
  const response = summaryRequest(data.authorization)
  latency.add(response.timings.duration)
  failures.add(response.status !== 200)
  check(response, { 'summary succeeds': (result) => result.status === 200 })
}
