namespace Core.Api.Application.Models;

public sealed record UpdateManagedUserRequest(string? Email, string Role, bool Enabled);
