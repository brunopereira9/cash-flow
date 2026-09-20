export type AuditRecord = {
    id: string;
    actorId: string;
    operation: string;
    correlationId: string;
    occurredAt: string;
    before?: unknown;
    after?: unknown
}
