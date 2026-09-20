using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace CashFlow.BuildingBlocks.Identity;

public sealed class KeycloakStateClient(
    IHttpClientFactory clients,
    IConfiguration configuration)
{
    public async Task<KeycloakState> GetAsync(
        ClaimsPrincipal principal,
        CancellationToken requestToken)
    {
        var subject = principal.FindFirstValue("sub")
            ?? throw new InvalidOperationException("JWT has no subject.");
        var authority = configuration["Keycloak:Authority"]
            ?? throw new InvalidOperationException("Keycloak authority is required.");
        var timeout = TimeSpan.FromSeconds(
            configuration.GetValue("Keycloak:CurrentStateTimeoutSeconds", 2));
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
        timeoutSource.CancelAfter(timeout);

        var client = clients.CreateClient("keycloak-current-state");
        var adminToken = await GetAdminTokenAsync(client, authority, timeoutSource.Token);
        var (baseUri, realm) = KeycloakRealm.Parse(authority);
        var headers = new AuthenticationHeaderValue("Bearer", adminToken);

        using var userRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUri}/admin/realms/{realm}/users/{Uri.EscapeDataString(subject)}");
        userRequest.Headers.Authorization = headers;
        using var userResponse = await client.SendAsync(userRequest, timeoutSource.Token);
        userResponse.EnsureSuccessStatusCode();
        using var user = JsonDocument.Parse(
            await userResponse.Content.ReadAsStringAsync(timeoutSource.Token));

        using var rolesRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUri}/admin/realms/{realm}/users/{Uri.EscapeDataString(subject)}/role-mappings/realm");
        rolesRequest.Headers.Authorization = headers;
        using var rolesResponse = await client.SendAsync(rolesRequest, timeoutSource.Token);
        rolesResponse.EnsureSuccessStatusCode();
        using var roles = JsonDocument.Parse(
            await rolesResponse.Content.ReadAsStringAsync(timeoutSource.Token));

        var roleNames = roles.RootElement
            .EnumerateArray()
            .Select(role => role.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        return new KeycloakState(
            user.RootElement.GetProperty("enabled").GetBoolean(),
            roleNames);
    }

    private async Task<string> GetAdminTokenAsync(
        HttpClient client,
        string authority,
        CancellationToken token)
    {
        var (baseUri, realm) = KeycloakRealm.Parse(authority);
        var clientId = configuration["Keycloak:AdminClientId"] ?? "cashflow-api";
        var clientSecret = configuration["Keycloak:AdminClientSecret"]
            ?? throw new InvalidOperationException("Keycloak admin client secret is required.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUri}/realms/{realm}/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret
            })
        };

        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(token));

        return body.RootElement.GetProperty("access_token").GetString()!;
    }
}

public sealed record KeycloakState(
    bool Enabled,
    IReadOnlySet<string> Roles);
