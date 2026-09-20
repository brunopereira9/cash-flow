using Core.Api.Application.Models;

namespace Core.Api.Application.Interfaces;

public interface IKeycloakAdminClient
{
    Task<IReadOnlyList<ManagedUser>> ListUsersAsync(CancellationToken cancellationToken);
    Task<ManagedUser?> CreateUserAsync(CreateManagedUserRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateUserAsync(string id, UpdateManagedUserRequest request, CancellationToken cancellationToken);
}
