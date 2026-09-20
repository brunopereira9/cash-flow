using System.Security.Claims;

namespace Summary.Api.Infrastructure.Identity;

public interface ICurrentKeycloakAuthorization { Task<CurrentKeycloakState> ConfirmAsync(ClaimsPrincipal principal, CancellationToken requestToken); }
