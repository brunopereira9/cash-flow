namespace Core.Api.Infrastructure.Identity;

public sealed record CurrentKeycloakState(bool Enabled, IReadOnlySet<string> Roles);
