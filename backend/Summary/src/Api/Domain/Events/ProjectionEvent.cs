namespace Summary.Api.Domain.Events;

public sealed record ProjectionEvent(Guid EventId, string Name, int Version, ProjectedEntryPayload Entry);
