using System.Net.Http.Json;
using System.Text.Json;
public class DailySummaryFreshnessTests : IClassFixture<SummaryApiFactory>
{
    private readonly HttpClient client;
    public DailySummaryFreshnessTests(SummaryApiFactory factory) => client = factory.CreateClient();
    [Fact]
    public async Task ReflectsConfirmedLedgerMutationWithinThirtySeconds()
    {
        var entryId = Guid.NewGuid();
        await client.PostAsJsonAsync("/internal/events", new { eventId = Guid.NewGuid(), name = "LedgerEntryCreated.v1", version = 1, entry = new { id = entryId, amount = 10m, type = "debit", description = "fresh", businessDate = "2026-09-19", version = 1, deleted = false } });
        await client.PostAsJsonAsync("/internal/events", new { eventId = Guid.NewGuid(), name = "LedgerEntryUpdated.v1", version = 2, entry = new { id = entryId, amount = 30m, type = "credit", description = "edited", businessDate = "2026-09-20", version = 2, deleted = false } });
        var oldDay = await (await client.GetAsync("/summary/daily/2026-09-19")).Content.ReadFromJsonAsync<JsonElement>();
        var summary = await (await client.GetAsync("/summary/daily/2026-09-20")).Content.ReadFromJsonAsync<JsonElement>();
        await client.PostAsJsonAsync("/internal/events", new { eventId = Guid.NewGuid(), name = "LedgerEntryDeleted.v1", version = 3, entry = new { id = entryId, amount = 30m, type = "credit", description = "edited", businessDate = "2026-09-20", version = 3, deleted = true } });
        var afterDelete = await (await client.GetAsync("/summary/daily/2026-09-20")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0m, oldDay.GetProperty("balance").GetDecimal());
        Assert.Equal(30m, summary.GetProperty("balance").GetDecimal());
        Assert.Equal(0m, afterDelete.GetProperty("balance").GetDecimal());
        Assert.Equal("current", summary.GetProperty("freshnessStatus").GetString());
        Assert.True(DateTimeOffset.UtcNow - summary.GetProperty("asOf").GetDateTimeOffset() < TimeSpan.FromSeconds(30));
    }
}
