namespace Core.Api.Application.Models;

public sealed record CreateManagedUserRequest(string Username, string? Email, string Role, bool Enabled = true);
