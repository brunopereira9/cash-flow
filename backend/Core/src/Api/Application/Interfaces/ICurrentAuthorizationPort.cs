namespace CashFlow.Core.Application;

using System.Security.Claims;

public interface ICurrentAuthorizationPort
{
    Task<CurrentAuthorizationSnapshot> ConfirmAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
