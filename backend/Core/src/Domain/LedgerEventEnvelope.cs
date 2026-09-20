namespace CashFlow.Core.Domain;

public sealed record LedgerEventEnvelope(Guid EventId, string Name, int Version, LedgerEventEntry Entry);
