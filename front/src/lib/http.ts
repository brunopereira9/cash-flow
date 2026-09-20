import { authorizedHeaders } from '../features/auth/oidc'

export class ApiError extends Error {
  readonly status: number
  readonly code?: string

  constructor(status: number, code?: string) {
    super(`API request failed with ${status}`)
    this.status = status
    this.code = code
  }
}

export async function jsonOrError<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let code: string | undefined
    try {
      const body = await response.clone().json() as { code?: string }
      code = body.code
    } catch {
      // Error responses are allowed to have an empty body.
    }
    throw new ApiError(response.status, code)
  }
  return response.json() as Promise<T>
}

export function apiUrl(name: 'core' | 'summary'): string {
  return name === 'core'
    ? (import.meta.env.VITE_CORE_URL || 'http://localhost:5080')
    : (import.meta.env.VITE_SUMMARY_URL || 'http://localhost:5081')
}

export function apiHeaders(accessToken: string, headers: HeadersInit = {}) {
  return authorizedHeaders(accessToken, headers)
}
