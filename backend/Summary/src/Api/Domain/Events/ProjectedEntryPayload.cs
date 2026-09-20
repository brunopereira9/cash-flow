namespace Summary.Api.Domain.Events;

public sealed record ProjectedEntryPayload(
    Guid Id,
    decimal Amount,
    string Type,
    string Description,
    DateOnly BusinessDate,
    int Version,
    bool Deleted);