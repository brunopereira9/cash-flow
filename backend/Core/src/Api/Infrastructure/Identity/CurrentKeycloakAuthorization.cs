using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Core.Api.Infrastructure.Identity;

public sealed class CurrentKeycloakAuthorization(IHttpClientFactory clients, IConfiguration configuration)
    : ICurrentKeycloakAuthorization
{
    public async Task<CurrentKeycloakState> ConfirmAsync(ClaimsPrincipal principal, CancellationToken requestToken)
    {
        CashFlowCoreTelemetry.KeycloakChecks.Add(1);
        var subject = principal.FindFirstValue("sub") ?? throw new InvalidOperationException("JWT has no subject.");
        var authority = configuration["Keycloak:Authority"] ??
                        throw new InvalidOperationException("Keycloak authority is required.");
        var timeout = TimeSpan.FromSeconds(configuration.GetValue("Keycloak:CurrentStateTimeoutSeconds", 2));
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
        timeoutSource.CancelAfter(timeout);
        var client = clients.CreateClient("keycloak-current-state");
        var token = await AdminTokenAsync(client, authority, timeoutSource.Token);
        var (baseUri, realm) = Realm(authority);
        using var userRequest = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUri}/admin/realms/{realm}/users/{Uri.EscapeDataString(subject)}");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var userResponse = await client.SendAsync(userRequest, timeoutSource.Token);
        userResponse.EnsureSuccessStatusCode();
        using var userDocument = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(timeoutSource.Token));
        var enabled = userDocument.RootElement.TryGetProperty("enabled", out var enabledValue) &&
                      enabledValue.GetBoolean();
        using var rolesRequest = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUri}/admin/realms/{realm}/users/{Uri.EscapeDataString(subject)}/role-mappings/realm");
        rolesRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var rolesResponse = await client.SendAsync(rolesRequest, timeoutSource.Token);
        rolesResponse.EnsureSuccessStatusCode();
        using var rolesDocument =
            JsonDocument.Parse(await rolesResponse.Content.ReadAsStringAsync(timeoutSource.Token));
        var roles = rolesDocument.RootElement.EnumerateArray().Select(role => role.GetProperty("name").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        return new CurrentKeycloakState(enabled, roles);
    }

    private async Task<string> AdminTokenAsync(HttpClient client, string authority, CancellationToken token)
    {
        var (baseUri, realm) = Realm(authority);
        var clientId = configuration["Keycloak:AdminClientId"] ?? "cashflow-api";
        var clientSecret = configuration["Keycloak:AdminClientSecret"] ??
                           throw new InvalidOperationException("Keycloak admin client secret is required.");
        using var request =
            new HttpRequestMessage(HttpMethod.Post, $"{baseUri}/realms/{realm}/protocol/openid-connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials", ["client_id"] = clientId, ["client_secret"] = clientSecret
                })
            };
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return document.RootElement.GetProperty("access_token").GetString()!;
    }

    private static (string BaseUri, string Realm) Realm(string authority)
    {
        var uri = new Uri(authority.TrimEnd('/'));
        var marker = "/realms/";
        var index = uri.AbsolutePath.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0) throw new InvalidOperationException("Keycloak authority must contain /realms/{realm}.");
        return ($"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath[..index]}",
            uri.AbsolutePath[(index + marker.Length)..]);
    }
}

public static class CurrentKeycloakAuthorizationApplicationBuilderExtensions
{
    public static IApplicationBuilder UseCurrentKeycloakAuthorization(this IApplicationBuilder app,
        IConfiguration configuration)
    {
        if (!configuration.GetValue("Keycloak:Enabled", false)) return app;
        return app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/ledger") &&
                !context.Request.Path.StartsWithSegments("/audit"))
            {
                await next();
                return;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                await context.ChallengeAsync();
                return;
            }

            try
            {
                var state = await context.RequestServices.GetRequiredService<ICurrentKeycloakAuthorization>()
                    .ConfirmAsync(context.User, context.RequestAborted);
                if (!state.Enabled || !HasBusinessRole(state.Roles) ||
                    (IsMutation(context.Request) && !HasWriteRole(state.Roles)))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                await next();
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                                  or InvalidOperationException or JsonException)
            {
                CashFlowCoreTelemetry.KeycloakFailures.Add(1);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
        });
    }

    private static bool HasBusinessRole(IReadOnlySet<string> roles) =>
        roles.Count(role => role is "admin" or "operator" or "auditor") == 1;

    private static bool HasWriteRole(IReadOnlySet<string> roles) =>
        roles.Contains("admin") || roles.Contains("operator");

    private static bool IsMutation(HttpRequest request) => request.Method is "POST" or "PUT" or "PATCH" or "DELETE";
}