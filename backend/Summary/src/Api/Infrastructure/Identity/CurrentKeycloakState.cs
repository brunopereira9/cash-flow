namespace Summary.Api.Infrastructure.Identity;

public sealed record CurrentKeycloakState(bool Enabled, IReadOnlySet<string> Roles);