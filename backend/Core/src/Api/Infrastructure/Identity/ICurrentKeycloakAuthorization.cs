namespace CashFlow.Core.Infrastructure;

using System.Security.Claims;
public interface ICurrentKeycloakAuthorization { Task<CurrentKeycloakState> ConfirmAsync(ClaimsPrincipal principal, CancellationToken requestToken); }
