namespace CashFlow.BuildingBlocks.Identity;

public static class KeycloakRolePolicy
{
    public static bool HasBusinessAccess(IReadOnlySet<string> roles) =>
        roles.Count(role => role is "admin" or "operator") == 1;

    public static bool CanWrite(IReadOnlySet<string> roles) =>
        roles.Contains("admin") || roles.Contains("operator");
}
