using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

public class DailySummaryFreshnessTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture rabbit;
    public DailySummaryFreshnessTests(RabbitMqFixture rabbit) => this.rabbit = rabbit;

    [Fact]
    public async Task ReflectsConfirmedLedgerMutationWithinThirtySeconds()
    {
        using var coreFactory = new CrossServiceCoreApiFactory(rabbit.Uri);
        using var summaryFactory = new SummaryApiFactory { RabbitUri = rabbit.Uri };
        using var coreClient = coreFactory.CreateClient();
        using var summaryClient = summaryFactory.CreateClient();
        var firstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo"));
        var movedDate = firstDate.AddDays(-1);

        using var create = new HttpRequestMessage(HttpMethod.Post, "/ledger/entries") { Content = JsonContent.Create(new { amount = 10m, type = "debit", description = "fresh", businessDate = firstDate }) };
        create.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        create.Headers.Add("X-Actor-Id", "freshness-actor");
        var created = await coreClient.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var entryId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await TestEventually.WaitForAsync(async () => await HasSummaryAsync(summaryClient, firstDate, credits: 0m, debits: 10m, balance: -10m), TimeSpan.FromSeconds(30));

        using var update = new HttpRequestMessage(HttpMethod.Put, $"/ledger/entries/{entryId}") { Content = JsonContent.Create(new { amount = 30m, type = "credit", description = "edited", businessDate = movedDate, version = 1 }) };
        update.Headers.Add("X-Actor-Id", "freshness-actor");
        var updated = await coreClient.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        await TestEventually.WaitForAsync(async () =>
            await HasSummaryAsync(summaryClient, firstDate, credits: 0m, debits: 0m, balance: 0m) &&
            await HasSummaryAsync(summaryClient, movedDate, credits: 30m, debits: 0m, balance: 30m), TimeSpan.FromSeconds(30));

        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/ledger/entries/{entryId}?version=2");
        delete.Headers.Add("X-Actor-Id", "freshness-actor");
        var deleted = await coreClient.SendAsync(delete);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await TestEventually.WaitForAsync(async () => await HasSummaryAsync(summaryClient, movedDate, credits: 0m, debits: 0m, balance: 0m), TimeSpan.FromSeconds(30));

        var final = await GetSummaryAsync(summaryClient, movedDate);
        Assert.Equal("current", final.GetProperty("freshnessStatus").GetString());
        Assert.True(DateTimeOffset.UtcNow - final.GetProperty("asOf").GetDateTimeOffset() < TimeSpan.FromSeconds(30));
    }

    private static async Task<bool> HasSummaryAsync(HttpClient client, DateOnly date, decimal credits, decimal debits, decimal balance)
    {
        var response = await client.GetAsync($"/summary/daily/{date:yyyy-MM-dd}");
        if (response.StatusCode != HttpStatusCode.OK) return false;
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>();
        return summary.GetProperty("credits").GetDecimal() == credits &&
            summary.GetProperty("debits").GetDecimal() == debits &&
            summary.GetProperty("balance").GetDecimal() == balance &&
            summary.GetProperty("freshnessStatus").GetString() == "current";
    }

    private static async Task<JsonElement> GetSummaryAsync(HttpClient client, DateOnly date) =>
        await (await client.GetAsync($"/summary/daily/{date:yyyy-MM-dd}")).Content.ReadFromJsonAsync<JsonElement>();
}
