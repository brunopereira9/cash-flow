using System.Security.Claims;
using Core.Api.Application.Models;

namespace Core.Api.Application.Interfaces;

public interface ICurrentAuthorizationPort
{
    Task<CurrentAuthorizationSnapshot> ConfirmAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);
}
