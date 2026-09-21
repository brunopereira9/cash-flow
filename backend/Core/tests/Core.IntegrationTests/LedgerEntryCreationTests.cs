using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Core.IntegrationTests;

public sealed class CoreApiFactory : MigratedCoreApiFactory
{
    private readonly string database = Path.Combine(Path.GetTempPath(), $"cashflow-core-{Guid.NewGuid():N}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Core", $"Data Source={database}");
        builder.UseSetting("RabbitMq:Enabled", "false");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CoreDbContext>().Database.Migrate();
        return host;
    }

    public async Task<T> ReadDbAsync<T>(Func<CoreDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CoreDbContext>());
    }
}

public sealed record DbCounts(int Entries, int Attempts, int Audits, int Outbox);

public class LedgerEntryCreationTests : IClassFixture<CoreApiFactory>
{
    private readonly HttpClient client;
    private readonly CoreApiFactory factory;
    public LedgerEntryCreationTests(CoreApiFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task AcceptsValidEntryAndDefaultsBusinessDate()
    {
        var response = await PostAsync(new { amount = 10.00m, type = "credit", description = "Salary" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var entry = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("credit", entry.GetProperty("type").GetString());
        var today = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo"));
        Assert.Equal(today, DateOnly.Parse(entry.GetProperty("businessDate").GetString()!));

        Assert.Equal(
            HttpStatusCode.Created,
            (await PostAsync(new
            {
                amount = 2m,
                type = "debit",
                description = "Today",
                businessDate = today
            })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Created,
            (await PostAsync(new
            {
                amount = 3.45m,
                type = "credit",
                description = "Past",
                businessDate = today.AddDays(-1)
            })).StatusCode);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(new { amount = 0m, type = "credit", description = "bad" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(new { amount = -1m, type = "credit", description = "bad" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(new { amount = 1.234m, type = "credit", description = "bad" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(new { amount = 2m, type = "", description = "bad" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(new { amount = 2m, type = "transfer", description = "bad" })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(new { amount = 2m, type = "credit", description = "" })).StatusCode);
        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            (await PostAsync(new
            {
                amount = 2m,
                type = "credit",
                description = "future",
                businessDate = today.AddDays(1)
            })).StatusCode);
    }

    [Fact]
    public async Task RejectsInvalidEntry()
    {
        var response = await PostAsync(new { amount = -1m, type = "", description = "" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task IsIdempotentByActorAndAttemptKey()
    {
        var key = Guid.NewGuid().ToString("N");
        var request = new { amount = 12.50m, type = "debit", description = "Rent" };
        var before = await ReadCountsAsync();
        var first = await PostAsync(request, key, "actor-1");
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var second = await PostAsync(request, key, "actor-1");
        var changed = await PostAsync(new { amount = 13m, type = "debit", description = "Rent" }, key, "actor-1");
        var otherActor = await PostAsync(request, key, "actor-2");
        var after = await ReadCountsAsync();
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherActor.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        var otherActorBody = await otherActor.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(firstBody.GetProperty("id").GetGuid(), secondBody.GetProperty("id").GetGuid());
        Assert.Equal(firstBody.GetProperty("id").GetGuid(), otherActorBody.GetProperty("id").GetGuid());
        Assert.Equal(before.Entries + 1, after.Entries);
        Assert.Equal(before.Attempts + 1, after.Attempts);
        Assert.Equal(before.Audits + 1, after.Audits);
        Assert.Equal(before.Outbox + 1, after.Outbox);
    }

    private async Task<HttpResponseMessage> PostAsync(object payload, string? key = null, string actor = "creation-actor")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/ledger/entries") { Content = JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        request.Headers.Add("X-Actor-Id", actor);
        return await client.SendAsync(request);
    }

    private Task<DbCounts> ReadCountsAsync() => factory.ReadDbAsync(async db => new DbCounts(
        await db.LedgerEntries.CountAsync(),
        await db.CreationAttempts.CountAsync(),
        await db.AuditRecords.CountAsync(),
        await db.OutboxEvents.CountAsync()));
}
