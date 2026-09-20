namespace CashFlow.Core.Application;

public sealed record CurrentAuthorizationSnapshot(bool Enabled, IReadOnlySet<string> Roles);
