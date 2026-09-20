namespace CashFlow.Summary.Infrastructure;

public sealed record CurrentKeycloakState(bool Enabled, IReadOnlySet<string> Roles);
