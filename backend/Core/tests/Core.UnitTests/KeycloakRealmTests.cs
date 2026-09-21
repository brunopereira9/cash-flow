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

    [Fact]
    public void AcceptsAuthorityWithTrailingSlash()
    {
        var result = KeycloakRealm.Parse("http://keycloak:8080/realms/cashflow/");

        Assert.Equal("http://keycloak:8080", result.BaseUri);
        Assert.Equal("cashflow", result.Realm);
    }

    [Theory]
    [InlineData("http://keycloak:8080/realms/")]
    [InlineData("http://keycloak:8080/realms/cashflow/admin")]
    public void RejectsAuthorityWithInvalidRealmPath(string authority)
    {
        Assert.Throws<InvalidOperationException>(() => KeycloakRealm.Parse(authority));
    }
}
