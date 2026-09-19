using System.Net;
using System.Net.Http.Json;
public class DailySummaryAvailabilityTests
{
    [Fact]
    public async Task NeverLabelsStaleOrUnavailableBalanceAsCurrent()
    {
        using var factory = new SummaryApiFactory { Available = false };
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/summary/daily/2026-09-19");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var unavailable = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("unavailable", unavailable.GetProperty("freshnessStatus").GetString());

        using var staleFactory = new StaleSummaryApiFactory();
        using var staleClient = staleFactory.CreateClient();
        await staleClient.PostAsJsonAsync("/internal/events", new { eventId = Guid.NewGuid(), name = "LedgerEntryCreated.v1", version = 1, entry = new { id = Guid.NewGuid(), amount = 10m, type = "credit", description = "old", businessDate = "2026-09-19", version = 1, deleted = false } });
        await Task.Delay(20);
        var stale = await (await staleClient.GetAsync("/summary/daily/2026-09-19")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("stale", stale.GetProperty("freshnessStatus").GetString());
    }
}

public sealed class StaleSummaryApiFactory : SummaryApiFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Summary:FreshnessSeconds", "0");
    }
}
