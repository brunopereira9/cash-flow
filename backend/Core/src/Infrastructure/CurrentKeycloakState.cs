namespace CashFlow.Core.Infrastructure;

public sealed record CurrentKeycloakState(bool Enabled, IReadOnlySet<string> Roles);
