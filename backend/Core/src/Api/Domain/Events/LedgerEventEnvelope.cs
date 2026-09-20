namespace Core.Api.Domain.Events;

public sealed record LedgerEventEnvelope(Guid EventId, string Name, int Version, LedgerEventEntry Entry);