using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        using (var scope = staleFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SummaryDbContext>();
            db.DailySummaries.Add(new DailySummary { Date = new DateOnly(2026, 9, 19), Credits = 10m, Balance = 10m, AsOf = DateTimeOffset.UtcNow.AddSeconds(-31) });
            await db.SaveChangesAsync();
        }
        var stale = await (await staleClient.GetAsync("/summary/daily/2026-09-19")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("stale", stale.GetProperty("freshnessStatus").GetString());
        Assert.Equal(10m, stale.GetProperty("balance").GetDecimal());
        Assert.NotEqual("current", stale.GetProperty("freshnessStatus").GetString());
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
