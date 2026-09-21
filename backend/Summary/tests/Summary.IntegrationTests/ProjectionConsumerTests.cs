using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Summary.Api.Infrastructure.Persistence;

namespace Summary.IntegrationTests;

public class SummaryApiFactory : MigratedSummaryApiFactory
{
    public bool Available { get; init; } = true;
    public string? RabbitUri { get; init; }
    public string DatabasePath { get; init; } = Path.Combine(Path.GetTempPath(), $"cashflow-summary-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Summary:Available", Available.ToString());
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Summary", $"Data Source={DatabasePath}");
        builder.UseSetting("RabbitMq:Enabled", (!string.IsNullOrWhiteSpace(RabbitUri)).ToString());
        if (!string.IsNullOrWhiteSpace(RabbitUri)) builder.UseSetting("RabbitMq:Uri", RabbitUri);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SummaryDbContext>().Database.Migrate();
        return host;
    }

    public async Task<T> ReadDbAsync<T>(Func<SummaryDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<SummaryDbContext>());
    }
}

[Collection("RabbitMQ integration")]
public class ProjectionConsumerTests
{
    private readonly RabbitMqFixture rabbit;
    public ProjectionConsumerTests(RabbitMqFixture rabbit) => this.rabbit = rabbit;

    [Fact]
    public async Task DeduplicatesRepeatedDelivery()
    {
        using var coreFactory = new CrossServiceCoreApiFactory(rabbit.Uri);
        using var summaryFactory = new SummaryApiFactory { RabbitUri = rabbit.Uri };
        using var coreClient = coreFactory.CreateClient();
        using var summaryClient = summaryFactory.CreateClient();
        var businessDate =
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo"));
        using var create = new HttpRequestMessage(HttpMethod.Post, "/ledger/entries")
        {
            Content = JsonContent.Create(new { amount = 25m, type = "credit", description = "once", businessDate })
        };
        create.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        create.Headers.Add("X-Actor-Id", "rabbit-actor");
        var created = await coreClient.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var entry = await created.Content.ReadFromJsonAsync<JsonElement>();
        var entryId = entry.GetProperty("id").GetGuid();

        await TestEventually.WaitForAsync(async () => await SummaryHasAmountAsync(summaryClient, businessDate, 25m),
            TimeSpan.FromSeconds(30));
        var coreState = await coreFactory.ReadDbAsync(async db => new
        {
            Entries = await db.LedgerEntries.CountAsync(x => x.Id == entryId),
            Audits = await db.AuditRecords.CountAsync(x => x.EntryId == entryId),
            Outbox = await db.OutboxEvents.SingleAsync(x => x.Payload.Contains(entryId.ToString())),
        });
        Assert.Equal(1, coreState.Entries);
        Assert.Equal(1, coreState.Audits);
        Assert.True(coreState.Outbox.PublishedAt.HasValue);

        var duplicate = await summaryClient.PostAsJsonAsync("/internal/events",
            JsonSerializer.Deserialize<JsonElement>(coreState.Outbox.Payload));
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        var inboxAfterDuplicate =
            await summaryFactory.ReadDbAsync(db =>
                db.InboxEvents.CountAsync(x => x.EventId == coreState.Outbox.EventId));
        Assert.Equal(1, inboxAfterDuplicate);

        using var update = new HttpRequestMessage(HttpMethod.Put, $"/ledger/entries/{entryId}")
        {
            Content = JsonContent.Create(new
            { amount = 40m, type = "credit", description = "updated", businessDate, version = 1 })
        };
        update.Headers.Add("X-Actor-Id", "rabbit-actor");
        var updated = await coreClient.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        await TestEventually.WaitForAsync(async () => await SummaryHasAmountAsync(summaryClient, businessDate, 40m),
            TimeSpan.FromSeconds(30));

        var obsolete = await summaryClient.PostAsJsonAsync("/internal/events",
            new
            {
                eventId = Guid.NewGuid(),
                name = "LedgerEntryUpdated.v1",
                version = 1,
                entry = new
                {
                    id = entryId,
                    amount = 99m,
                    type = "credit",
                    description = "obsolete",
                    businessDate,
                    version = 1,
                    deleted = false
                }
            });
        Assert.Equal(HttpStatusCode.OK, obsolete.StatusCode);
        var projected = await summaryFactory.ReadDbAsync(db => db.ProjectedEntries.SingleAsync(x => x.Id == entryId));
        Assert.Equal(2, projected.Version);
        Assert.Equal(40m, projected.Amount);
    }

    private static async Task<bool> SummaryHasAmountAsync(HttpClient client, DateOnly date, decimal amount)
    {
        var response = await client.GetAsync($"/summary/daily/{date:yyyy-MM-dd}");
        if (response.StatusCode != HttpStatusCode.OK) return false;
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>();
        return summary.GetProperty("credits").GetDecimal() == amount &&
               summary.GetProperty("freshnessStatus").GetString() == "current";
    }
}
