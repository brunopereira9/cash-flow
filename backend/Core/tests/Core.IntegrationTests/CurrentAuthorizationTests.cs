using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Core.IntegrationTests;

public class CurrentAuthorizationTests : IClassFixture<SecuredCoreApiFactory>
{
    private readonly SecuredCoreApiFactory factory;
    public CurrentAuthorizationTests(SecuredCoreApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task FailsClosedWhenKeycloakCannotConfirmState()
    {
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", AuthenticationTests.Token(DateTime.UtcNow.AddMinutes(5))); client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var started = Stopwatch.GetTimestamp();
        var response = await client.PostAsJsonAsync("/ledger/entries", new { amount = 10m, type = "credit", description = "Keycloak unavailable" });
        var elapsed = Stopwatch.GetElapsedTime(started);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("2", factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Keycloak:CurrentStateTimeoutSeconds"]);
        Assert.InRange(elapsed, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(4.5));
        Assert.Equal((0, 0, 0), await factory.MutationCountsAsync());
    }
}

public sealed class ProtocolCurrentKeycloakState
{
    public bool Enabled { get; set; } = true;
    public string Role { get; set; } = "operator";
}

internal sealed class KeycloakProtocolHandler(Func<ProtocolCurrentKeycloakState> state) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var current = state();
        var path = request.RequestUri?.AbsolutePath ?? "";
        var body = path.EndsWith("/token", StringComparison.Ordinal)
            ? "{\"access_token\":\"protocol-admin-token\"}"
            : path.EndsWith("/role-mappings/realm", StringComparison.Ordinal)
                ? $"[{{\"name\":\"{current.Role}\"}}]"
                : path.Contains("/admin/realms/", StringComparison.Ordinal)
                    ? $"{{\"enabled\":{current.Enabled.ToString().ToLowerInvariant()}}}"
                    : "{}";
        var response = new HttpResponseMessage(path.Contains("/admin/realms/", StringComparison.Ordinal) || path.EndsWith("/token", StringComparison.Ordinal) ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        return Task.FromResult(response);
    }
}

public sealed class CurrentStateCoreApiFactory : MigratedCoreApiFactory
{
    public ProtocolCurrentKeycloakState State { get; } = new();
    private readonly string database = Path.Combine(Path.GetTempPath(), $"cashflow-current-state-{Guid.NewGuid():N}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "Sqlite"); builder.UseSetting("ConnectionStrings:Core", $"Data Source={database}"); builder.UseSetting("RabbitMq:Enabled", "false");
        builder.UseSetting("Keycloak:Enabled", "true"); builder.UseSetting("Keycloak:Authority", "http://keycloak.test/realms/cashflow"); builder.UseSetting("Keycloak:Audience", SecuredCoreApiFactory.Audience); builder.UseSetting("Keycloak:ValidationSigningKey", SecuredCoreApiFactory.SigningKey); builder.UseSetting("Keycloak:AdminClientSecret", "test-secret");
        builder.ConfigureServices(services => services.AddHttpClient("keycloak-current-state").ConfigurePrimaryHttpMessageHandler(() => new KeycloakProtocolHandler(() => State)));
    }
}

public class CurrentRoleTests : IClassFixture<CurrentStateCoreApiFactory>
{
    private readonly CurrentStateCoreApiFactory factory;
    public CurrentRoleTests(CurrentStateCoreApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task AppliesDisabledAccountAndRoleChangeOnTheNextRequestWithTheSameJwt()
    {
        var token = AuthenticationTests.Token(DateTime.UtcNow.AddMinutes(5), issuer: "http://keycloak.test/realms/cashflow");
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/ledger/entries")).StatusCode);
        factory.State.Enabled = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/ledger/entries")).StatusCode);
        factory.State.Enabled = true; factory.State.Role = "auditor";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/ledger/entries")).StatusCode);
    }
}