export type UserRole = 'admin' | 'operator' | 'auditor'
export type UserSummary = { id: string; username: string; email?: string; role: UserRole; active: boolean }
