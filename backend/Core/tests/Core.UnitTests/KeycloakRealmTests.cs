using CashFlow.BuildingBlocks.Identity;

namespace Core.UnitTests;

public sealed class KeycloakRealmTests
{
    [Fact]
    public void ParsesBaseUriAndRealmFromAuthority()
    {
        var result = KeycloakRealm.Parse("http://keycloak:8080/realms/cashflow");

        Assert.Equal("http://keycloak:8080", result.BaseUri);
        Assert.Equal("cashflow", result.Realm);
    }

    [Fact]
    public void RejectsAuthorityWithoutRealmSegment()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => KeycloakRealm.Parse("http://keycloak:8080"));

        Assert.Contains("/realms/{realm}", exception.Message, StringComparison.Ordinal);
    }
}
