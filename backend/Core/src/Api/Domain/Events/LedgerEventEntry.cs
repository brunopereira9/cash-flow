namespace Core.Api.Domain.Events;

public sealed record LedgerEventEntry(
    Guid Id,
    decimal Amount,
    string Type,
    string Description,
    DateOnly BusinessDate,
    int Version,
    bool Deleted);