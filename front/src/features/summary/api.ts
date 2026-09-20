import { apiHeaders, apiUrl, jsonOrError } from '../../lib/http'
import type { DailySummary } from './types'

export function getDailySummary(accessToken: string, date: string): Promise<DailySummary> {
  return fetch(`${apiUrl('summary')}/summary/daily/${date}`, { headers: apiHeaders(accessToken) }).then(jsonOrError<DailySummary>)
}
