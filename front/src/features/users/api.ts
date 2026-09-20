import { apiHeaders, apiUrl, jsonOrError } from '../../lib/http'
import type { UserSummary } from './types'

export function listUsers(accessToken: string): Promise<UserSummary[]> {
  return fetch(`${apiUrl('core')}/identity/users`, { headers: apiHeaders(accessToken) })
    .then(jsonOrError<Array<UserSummary & { enabled?: boolean }>>)
    .then((users) => users.map(({ enabled, ...user }) => ({ ...user, active: enabled ?? user.active })))
}

export function updateUser(accessToken: string, id: string, input: Pick<UserSummary, 'email' | 'role' | 'active'>): Promise<void> {
  return fetch(`${apiUrl('core')}/identity/users/${encodeURIComponent(id)}`, {
    method: 'PUT',
    headers: apiHeaders(accessToken, { 'Content-Type': 'application/json' }),
    body: JSON.stringify({ email: input.email, role: input.role, enabled: input.active }),
  }).then(async (response) => { if (!response.ok) throw new Error(`User update failed with ${response.status}`) })
}
