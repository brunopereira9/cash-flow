namespace CashFlow.Core.Application;

public sealed record CreateManagedUserRequest(string Username, string? Email, string Role, bool Enabled = true);
