using System.Security.Claims;

namespace Core.Api.Infrastructure.Identity;

public interface ICurrentKeycloakAuthorization
{
    Task<CurrentKeycloakState> ConfirmAsync(ClaimsPrincipal principal, CancellationToken requestToken);
}