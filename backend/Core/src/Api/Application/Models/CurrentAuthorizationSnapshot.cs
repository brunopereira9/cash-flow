namespace Core.Api.Application.Models;

public sealed record CurrentAuthorizationSnapshot(bool Enabled, IReadOnlySet<string> Roles);
