export type Freshness = 'current' | 'stale' | 'unavailable'
export type DailySummary = { credits: number; debits: number; balance: number; freshnessStatus: Freshness; asOf?: string }
