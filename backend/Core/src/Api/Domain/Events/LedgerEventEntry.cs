namespace CashFlow.Core.Domain;

public sealed record LedgerEventEntry(Guid Id, decimal Amount, string Type, string Description, DateOnly BusinessDate, int Version, bool Deleted);
