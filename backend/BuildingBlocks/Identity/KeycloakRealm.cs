namespace CashFlow.BuildingBlocks.Identity;

public static class KeycloakRealm
{
    public static (string BaseUri, string Realm) Parse(string authority)
    {
        var uri = new Uri(authority.TrimEnd('/'));
        const string marker = "/realms/";
        var index = uri.AbsolutePath.IndexOf(marker, StringComparison.Ordinal);

        if (index < 0)
        {
            throw new InvalidOperationException(
                "Keycloak authority must contain /realms/{realm}.");
        }

        return (
            $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath[..index]}",
            uri.AbsolutePath[(index + marker.Length)..]);
    }
}
