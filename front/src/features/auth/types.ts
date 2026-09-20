export type AuthState =
  | { kind: 'loading' }
  | { kind: 'error'; message: string }
  | { kind: 'authenticated'; accessToken: string }
