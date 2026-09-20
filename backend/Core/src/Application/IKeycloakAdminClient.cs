namespace CashFlow.Core.Application;

public interface IKeycloakAdminClient
{
    Task<IReadOnlyList<ManagedUser>> ListUsersAsync(CancellationToken cancellationToken);
    Task<ManagedUser?> CreateUserAsync(CreateManagedUserRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateUserAsync(string id, UpdateManagedUserRequest request, CancellationToken cancellationToken);
}
