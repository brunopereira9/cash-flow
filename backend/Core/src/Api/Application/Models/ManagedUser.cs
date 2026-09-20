namespace CashFlow.Core.Application;

public sealed record ManagedUser(string Id, string Username, string? Email, bool Enabled, string Role);
