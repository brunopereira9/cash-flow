using System.Security.Claims;

public sealed record CurrentAuthorizationSnapshot(bool Enabled, IReadOnlySet<string> Roles);

public interface ICurrentAuthorizationPort
{
    Task<CurrentAuthorizationSnapshot> ConfirmAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
