using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Summary.IntegrationTests;

public sealed class ProtocolSummaryKeycloakState
{
    public bool Enabled { get; set; } = true;
    public string Role { get; set; } = "operator";
}

internal sealed class SummaryKeycloakProtocolHandler(Func<ProtocolSummaryKeycloakState> state) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = state();
        var path = request.RequestUri?.AbsolutePath ?? "";
        if (request.Method == HttpMethod.Post &&
            path.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal))
        {
            var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (request.Content.Headers.ContentType?.MediaType != "application/x-www-form-urlencoded"
                || form["grant_type"] != "client_credentials"
                || form["client_id"] != "cashflow-api"
                || form["client_secret"] != "test-secret") return Response(HttpStatusCode.BadRequest, "{}");

            return Response(HttpStatusCode.OK,
                "{\"access_token\":\"protocol-admin-token\",\"token_type\":\"Bearer\",\"expires_in\":300}");
        }

        if (request.Method == HttpMethod.Get && path.EndsWith("/role-mappings/realm", StringComparison.Ordinal))
        {
            if (!HasAdminBearer(request)) return Response(HttpStatusCode.Unauthorized, "{}");
            return Response(HttpStatusCode.OK,
                $"[{{\"id\":\"operator-role\",\"name\":\"{current.Role}\",\"containerId\":\"cashflow\"}}]");
        }

        if (request.Method == HttpMethod.Get &&
            path.Contains("/admin/realms/cashflow/users/", StringComparison.Ordinal))
        {
            if (!HasAdminBearer(request)) return Response(HttpStatusCode.Unauthorized, "{}");
            return Response(HttpStatusCode.OK,
                $"{{\"id\":\"demo-operator\",\"username\":\"demo-operator\",\"enabled\":{current.Enabled.ToString().ToLowerInvariant()}}}");
        }

        return Response(HttpStatusCode.NotFound, "{}");
    }

    private static bool HasAdminBearer(HttpRequestMessage request) =>
        request.Headers.Authorization?.Scheme == "Bearer" &&
        request.Headers.Authorization.Parameter == "protocol-admin-token";

    private static HttpResponseMessage Response(HttpStatusCode status, string body) => new(status)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}

public sealed class SecuredSummaryApiFactory : MigratedSummaryApiFactory
{
    public const string Issuer = "http://keycloak.test/realms/cashflow";
    public const string Audience = "cashflow-front";
    public const string SigningKey = "cashflow-summary-integration-signing-key";
    public ProtocolSummaryKeycloakState State { get; } = new();

    private readonly string database =
        Path.Combine(Path.GetTempPath(), $"cashflow-secured-summary-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Summary", $"Data Source={database}");
        builder.UseSetting("RabbitMq:Enabled", "false");
        builder.UseSetting("Keycloak:Enabled", "true");
        builder.UseSetting("Keycloak:Authority", Issuer);
        builder.UseSetting("Keycloak:Audience", Audience);
        builder.UseSetting("Keycloak:ValidationSigningKey", SigningKey);
        builder.UseSetting("Keycloak:AdminClientId", "cashflow-api");
        builder.UseSetting("Keycloak:AdminClientSecret", "test-secret");
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient("keycloak-current-state")
                .ConfigurePrimaryHttpMessageHandler(() => new SummaryKeycloakProtocolHandler(() => State));
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme,
                options => options.MapInboundClaims = false);
        });
    }
}

public class SummaryCurrentAuthorizationTests : IClassFixture<SecuredSummaryApiFactory>
{
    private readonly SecuredSummaryApiFactory factory;
    public SummaryCurrentAuthorizationTests(SecuredSummaryApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task AppliesCurrentAccountStateAndRoleToTheNextSummaryRequest()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", Token());
        var initial = await client.GetAsync("/summary/daily/2026-09-19");
        if (!initial.IsSuccessStatusCode)
            Assert.Fail($"{initial.StatusCode}: {await initial.Content.ReadAsStringAsync()}");
        factory.State.Enabled = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/summary/daily/2026-09-19")).StatusCode);
        factory.State.Enabled = true;
        factory.State.Role = "auditor";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/summary/daily/2026-09-19")).StatusCode);
    }

    private static string Token() => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        issuer: SecuredSummaryApiFactory.Issuer, audience: SecuredSummaryApiFactory.Audience,
        claims: [new Claim("sub", "demo-operator")], notBefore: DateTime.UtcNow.AddMinutes(-1),
        expires: DateTime.UtcNow.AddMinutes(5),
        signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecuredSummaryApiFactory.SigningKey)),
            SecurityAlgorithms.HmacSha256)));
}