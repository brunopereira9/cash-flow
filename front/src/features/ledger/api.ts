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

export async function seedLedgerEntries(accessToken: string, total = 2000, days = 10): Promise<number> {
  const entriesPerDay = total / days
  const today = new Date()
  today.setHours(0, 0, 0, 0)
  const startDate = new Date(today)
  startDate.setDate(startDate.getDate() - (days - 1))

  for (let index = 0; index < total; index += 20) {
    const batch = Array.from({ length: Math.min(20, total - index) }, (_, offset) => {
      const entryIndex = index + offset
      const businessDate = new Date(startDate)
      businessDate.setDate(startDate.getDate() + Math.floor(entryIndex / entriesPerDay))
      const credit = entryIndex % 2 === 0

      return createLedgerEntry(accessToken, {
        amount: credit ? 100 : 50,
        type: credit ? 'credit' : 'debit',
        description: `Carga de teste ${entryIndex + 1}/${total}`,
        businessDate: businessDate.toISOString().slice(0, 10),
      }, `frontend-seed-${crypto.randomUUID()}`)
    })

    await Promise.all(batch)
  }

  return total
}
