import { apiHeaders, apiUrl, jsonOrError } from '../../lib/http'
import type { LedgerEntry } from './types'

export function listLedgerEntries(accessToken: string, date?: string): Promise<LedgerEntry[]> {
  const query = date ? `?date=${encodeURIComponent(date)}` : ''
  return fetch(`${apiUrl('core')}/ledger/entries${query}`, { headers: apiHeaders(accessToken) }).then(jsonOrError<LedgerEntry[]>)
}

export function createLedgerEntry(
  accessToken: string,
  input: Pick<LedgerEntry, 'amount' | 'type' | 'description'> & Partial<Pick<LedgerEntry, 'businessDate'>>,
  idempotencyKey = crypto.randomUUID(),
): Promise<LedgerEntry> {
  return fetch(`${apiUrl('core')}/ledger/entries`, {
    method: 'POST',
    headers: apiHeaders(accessToken, { 'Content-Type': 'application/json', 'Idempotency-Key': idempotencyKey }),
    body: JSON.stringify(input),
  }).then(jsonOrError<LedgerEntry>)
}
