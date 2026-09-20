namespace CashFlow.Summary.Domain;

public sealed record ProjectionEvent(Guid EventId, string Name, int Version, ProjectedEntryPayload Entry);
