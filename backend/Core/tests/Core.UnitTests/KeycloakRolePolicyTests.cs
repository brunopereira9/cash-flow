using CashFlow.BuildingBlocks.Identity;

namespace Core.UnitTests;

public sealed class KeycloakRolePolicyTests
{
    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public void GrantsBusinessAccessToSingleOperationalRole(string role)
    {
        var roles = new HashSet<string>([role], StringComparer.Ordinal);

        Assert.True(KeycloakRolePolicy.HasBusinessAccess(roles));
        Assert.True(KeycloakRolePolicy.CanWrite(roles));
    }

    [Fact]
    public void DeniesBusinessAccessToAuditor()
    {
        var roles = new HashSet<string>(["auditor"], StringComparer.Ordinal);

        Assert.False(KeycloakRolePolicy.HasBusinessAccess(roles));
        Assert.False(KeycloakRolePolicy.CanWrite(roles));
    }

    [Fact]
    public void DeniesBusinessAccessWhenMultipleOperationalRolesAreAssigned()
    {
        var roles = new HashSet<string>(["admin", "operator"], StringComparer.Ordinal);

        Assert.False(KeycloakRolePolicy.HasBusinessAccess(roles));
        Assert.True(KeycloakRolePolicy.CanWrite(roles));
    }
}
