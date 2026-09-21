using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Core.Api.Application.Interfaces;
using Core.Api.Application.Models;

namespace Core.Api.Infrastructure.Identity;

public sealed class KeycloakAdminClient(IHttpClientFactory clients, IConfiguration configuration) : IKeycloakAdminClient
{
    public async Task<IReadOnlyList<ManagedUser>> ListUsersAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, "users", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var result = new List<ManagedUser>();
        foreach (var user in document.RootElement.EnumerateArray())
        {
            var id = user.GetProperty("id").GetString()!;
            var roles = await RolesAsync(id, cancellationToken);
            result.Add(new ManagedUser(id, user.GetProperty("username").GetString() ?? "",
                user.TryGetProperty("email", out var email) ? email.GetString() : null,
                user.GetProperty("enabled").GetBoolean(),
                roles.Contains("admin")
                    ? "admin"
                    : roles.FirstOrDefault(role => role is "operator" or "auditor") ?? ""));
        }

        return result;
    }

    public async Task<ManagedUser?> CreateUserAsync(CreateManagedUserRequest request,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        { username = request.Username, email = request.Email, enabled = request.Enabled, emailVerified = false });
        using var response = await SendAsync(HttpMethod.Post, "users",
            new StringContent(payload, Encoding.UTF8, "application/json"), cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict) return null;
        response.EnsureSuccessStatusCode();
        var location = response.Headers.Location?.Segments.LastOrDefault()?.Trim('/');
        if (string.IsNullOrWhiteSpace(location)) return null;
        var role = await RoleAsync(request.Role, cancellationToken);
        using var roleResponse = await SendAsync(HttpMethod.Post,
            $"users/{Uri.EscapeDataString(location)}/role-mappings/realm",
            new StringContent(JsonSerializer.Serialize(new[] { role }), Encoding.UTF8, "application/json"),
            cancellationToken);
        roleResponse.EnsureSuccessStatusCode();
        return new ManagedUser(location, request.Username, request.Email, request.Enabled, request.Role);
    }

    public async Task<bool> UpdateUserAsync(string id, UpdateManagedUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Enabled || request.Role != "admin")
        {
            var users = await ListUsersAsync(cancellationToken);
            var activeAdmins = users.Count(user => user.Enabled && user.Role == "admin");
            var targetIsOnlyAdmin = users.Any(user => user.Id == id && user.Enabled && user.Role == "admin") &&
                                    activeAdmins <= 1;
            if (targetIsOnlyAdmin) throw new LastActiveAdminException();
        }

        var payload = JsonSerializer.Serialize(new { email = request.Email, enabled = request.Enabled });

        using var response = await SendAsync(HttpMethod.Put, $"users/{Uri.EscapeDataString(id)}",
            new StringContent(payload, Encoding.UTF8, "application/json"), cancellationToken);

        if (!response.IsSuccessStatusCode) return false;

        var currentRoles = await RealmRolesAsync(id, cancellationToken);
        if (currentRoles.Count > 0)
        {
            using var removeRolesResponse = await SendAsync(
                HttpMethod.Delete,
                $"users/{Uri.EscapeDataString(id)}/role-mappings/realm",
                new StringContent(JsonSerializer.Serialize(currentRoles), Encoding.UTF8, "application/json"),
                cancellationToken);
            if (!removeRolesResponse.IsSuccessStatusCode) return false;
        }

        var role = await RoleAsync(request.Role, cancellationToken);

        using var roleResponse = await SendAsync(HttpMethod.Post,
            $"users/{Uri.EscapeDataString(id)}/role-mappings/realm",
            new StringContent(JsonSerializer.Serialize(new[] { role }), Encoding.UTF8, "application/json"),
            cancellationToken);
        return roleResponse.IsSuccessStatusCode;
    }

    private async Task<JsonElement> RoleAsync(string role, CancellationToken cancellationToken)
    {
        using var response =
            await SendAsync(HttpMethod.Get, $"roles/{Uri.EscapeDataString(role)}", null, cancellationToken);

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        return document.RootElement.Clone();
    }

    private async Task<HashSet<string>> RolesAsync(string id, CancellationToken cancellationToken)
    {
        var roles = await RealmRolesAsync(id, cancellationToken);
        return roles.Select(item => item.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private async Task<List<JsonElement>> RealmRolesAsync(string id, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, $"users/{Uri.EscapeDataString(id)}/role-mappings/realm",
            null, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.EnumerateArray().Select(item => item.Clone()).ToList();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content,
        CancellationToken cancellationToken)
    {
        var authority = configuration["Keycloak:Authority"] ??
                        throw new InvalidOperationException("Keycloak authority is required.");
        var (baseUri, realm) = Realm(authority);
        var client = clients.CreateClient("keycloak-current-state");
        var token = await AdminTokenAsync(client, baseUri, realm, cancellationToken);
        var request = new HttpRequestMessage(method, $"{baseUri}/admin/realms/{realm}/{path}") { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, cancellationToken);
    }

    private async Task<string> AdminTokenAsync(HttpClient client, string baseUri, string realm,
        CancellationToken cancellationToken)
    {
        var clientId = configuration["Keycloak:UserAdminClientId"] ??
                       throw new InvalidOperationException("Keycloak user-admin client id is required.");
        var clientSecret = configuration["Keycloak:UserAdminClientSecret"] ??
                           throw new InvalidOperationException("Keycloak user-admin client secret is required.");
        using var request =
            new HttpRequestMessage(HttpMethod.Post, $"{baseUri}/realms/{realm}/protocol/openid-connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret
                })
            };
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("access_token").GetString()!;
    }

    private static (string BaseUri, string Realm) Realm(string authority)
    {
        var uri = new Uri(authority.TrimEnd('/'));
        const string marker = "/realms/";
        var index = uri.AbsolutePath.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0) throw new InvalidOperationException("Keycloak authority must contain /realms/{realm}.");
        return ($"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath[..index]}",
            uri.AbsolutePath[(index + marker.Length)..]);
    }
}
