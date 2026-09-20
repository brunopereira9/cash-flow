namespace CashFlow.Core.Application;

public sealed record UpdateManagedUserRequest(string? Email, string Role, bool Enabled);
