import { apiHeaders, apiUrl, jsonOrError } from '../../lib/http'
import type { AuditRecord } from './types'

export type AuditFilters = { actor?: string; operation?: string; correlation?: string }

export function listAuditRecords(accessToken: string, filters: AuditFilters = {}): Promise<AuditRecord[]> {
  const query = new URLSearchParams(Object.entries(filters).filter((entry): entry is [string, string] => Boolean(entry[1])))
  return fetch(`${apiUrl('core')}/audit${query.size ? `?${query}` : ''}`, { headers: apiHeaders(accessToken) }).then(jsonOrError<AuditRecord[]>)
}
