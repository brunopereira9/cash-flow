using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Summary.Api.Infrastructure.Identity;

public sealed class CurrentKeycloakAuthorization(IHttpClientFactory clients, IConfiguration configuration)
    : ICurrentKeycloakAuthorization
{
    public async Task<CurrentKeycloakState> ConfirmAsync(ClaimsPrincipal principal, CancellationToken requestToken)
    {
        CashFlowSummaryTelemetry.KeycloakChecks.Add(1);
        var subject = principal.FindFirstValue("sub") ?? throw new InvalidOperationException("JWT has no subject.");
        var authority = configuration["Keycloak:Authority"] ??
                        throw new InvalidOperationException("Keycloak authority is required.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(configuration.GetValue("Keycloak:CurrentStateTimeoutSeconds", 2)));
        var client = clients.CreateClient("keycloak-current-state");
        var adminToken = await AdminTokenAsync(client, authority, timeout.Token);
        var (baseUri, realm) = Realm(authority);
        var headers = new AuthenticationHeaderValue("Bearer", adminToken);
        using var userRequest = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUri}/admin/realms/{realm}/users/{Uri.EscapeDataString(subject)}");
        userRequest.Headers.Authorization = headers;
        using var userResponse = await client.SendAsync(userRequest, timeout.Token);
        userResponse.EnsureSuccessStatusCode();
        using var user = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(timeout.Token));
        using var rolesRequest = new HttpRequestMessage(HttpMethod.Get,
            $"{baseUri}/admin/realms/{realm}/users/{Uri.EscapeDataString(subject)}/role-mappings/realm");
        rolesRequest.Headers.Authorization = headers;
        using var rolesResponse = await client.SendAsync(rolesRequest, timeout.Token);
        rolesResponse.EnsureSuccessStatusCode();
        using var roles = JsonDocument.Parse(await rolesResponse.Content.ReadAsStringAsync(timeout.Token));
        return new CurrentKeycloakState(user.RootElement.GetProperty("enabled").GetBoolean(),
            roles.RootElement.EnumerateArray().Select(x => x.GetProperty("name").GetString()!)
                .ToHashSet(StringComparer.Ordinal));
    }

    private async Task<string> AdminTokenAsync(HttpClient client, string authority, CancellationToken token)
    {
        var (baseUri, realm) = Realm(authority);
        using var request =
            new HttpRequestMessage(HttpMethod.Post, $"{baseUri}/realms/{realm}/protocol/openid-connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = configuration["Keycloak:AdminClientId"] ?? "cashflow-api",
                    ["client_secret"] = configuration["Keycloak:AdminClientSecret"] ??
                                        throw new InvalidOperationException("Keycloak admin client secret is required.")
                })
            };
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return body.RootElement.GetProperty("access_token").GetString()!;
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

public static class CurrentKeycloakAuthorizationApplicationBuilderExtensions
{
    public static IApplicationBuilder UseCurrentKeycloakAuthorization(this IApplicationBuilder app,
        IConfiguration configuration)
    {
        if (!configuration.GetValue("Keycloak:Enabled", false)) return app;
        return app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments("/summary"))
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
                if (!state.Enabled || state.Roles.Count(role => role is "admin" or "operator" or "auditor") != 1)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                await next();
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                                  or InvalidOperationException or JsonException)
            {
                CashFlowSummaryTelemetry.KeycloakFailures.Add(1);
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
        });
    }
}