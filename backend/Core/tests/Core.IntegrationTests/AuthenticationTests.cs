using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Core.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Core.IntegrationTests;

public sealed class SecuredCoreApiFactory : MigratedCoreApiFactory
{
    public const string Issuer = "http://127.0.0.1:1/realms/cashflow";
    public const string Audience = "cashflow-front";
    public const string SigningKey = "cashflow-integration-test-signing-key-32";
    private readonly string database = Path.Combine(
        Path.GetTempPath(),
        $"cashflow-secured-core-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Core", $"Data Source={database}");
        builder.UseSetting("RabbitMq:Enabled", "false");
        builder.UseSetting("Keycloak:Enabled", "true");
        builder.UseSetting("Keycloak:Authority", Issuer);
        builder.UseSetting("Keycloak:Audience", Audience);
        builder.UseSetting("Keycloak:ValidationSigningKey", SigningKey);
        builder.UseSetting("Keycloak:CurrentStateTimeoutSeconds", "2");
        builder.UseSetting("Keycloak:AdminClientSecret", "test-secret");
        builder.ConfigureServices(services =>
            services.AddHttpClient("keycloak-current-state")
                .ConfigurePrimaryHttpMessageHandler(() => new KeycloakTimeoutHandler()));
    }

    public async Task<(int Entries, int Audits, int Outbox)> MutationCountsAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        return (
            await db.LedgerEntries.CountAsync(),
            await db.AuditRecords.CountAsync(),
            await db.OutboxEvents.CountAsync());
    }
}

internal sealed class KeycloakTimeoutHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new OperationCanceledException(cancellationToken);
    }
}

public class AuthenticationTests : IClassFixture<SecuredCoreApiFactory>
{
    private readonly SecuredCoreApiFactory factory;
    public AuthenticationTests(SecuredCoreApiFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task RejectsInvalidJwtWithoutMutation()
    {
        var tokens = new[]
        {
            "not-a-jwt",
            Token(DateTime.UtcNow.AddMinutes(-1)),
            Token(DateTime.UtcNow.AddMinutes(5), issuer: "http://wrong/realms/cashflow"),
            Token(DateTime.UtcNow.AddMinutes(5), audience: "wrong-audience")
        };

        foreach (var token in tokens)
        {
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);
            client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            var response = await client.PostAsJsonAsync(
                "/ledger/entries",
                new { amount = 10m, type = "credit", description = "must not persist" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        Assert.Equal((0, 0, 0), await factory.MutationCountsAsync());
    }

    internal static string Token(
        DateTime expires,
        string? issuer = null,
        string? audience = null)
    {
        var token = new JwtSecurityToken(
            issuer: issuer ?? SecuredCoreApiFactory.Issuer,
            audience: audience ?? SecuredCoreApiFactory.Audience,
            claims: [new Claim("sub", "demo-operator")],
            notBefore: expires < DateTime.UtcNow
                ? expires.AddMinutes(-5)
                : DateTime.UtcNow.AddMinutes(-1),
            expires: expires,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(SecuredCoreApiFactory.SigningKey)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
