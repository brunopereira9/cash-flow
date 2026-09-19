using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
public class SummaryApiFactory : WebApplicationFactory<Program>
{
    public bool Available { get; init; } = true;
    private readonly string database = Path.Combine(Path.GetTempPath(), $"cashflow-summary-{Guid.NewGuid():N}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Summary:Available", Available.ToString());
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Summary", $"Data Source={database}");
        builder.UseSetting("RabbitMq:Enabled", "false");
    }
}
public class ProjectionConsumerTests : IClassFixture<SummaryApiFactory>
{
    private readonly HttpClient client;
    public ProjectionConsumerTests(SummaryApiFactory factory) => client = factory.CreateClient();
    [Fact]
    public async Task DeduplicatesRepeatedDelivery()
    {
        var entryId = Guid.NewGuid();
        var body = new { eventId = Guid.NewGuid(), name = "LedgerEntryCreated.v1", version = 1, entry = new { id = entryId, amount = 25m, type = "credit", description = "once", businessDate = "2026-09-19", version = 1, deleted = false } };
        var first = await client.PostAsJsonAsync("/internal/events", body);
        var second = await client.PostAsJsonAsync("/internal/events", body);
        var obsolete = await client.PostAsJsonAsync("/internal/events", new { eventId = Guid.NewGuid(), name = "LedgerEntryUpdated.v1", version = 1, entry = new { id = entryId, amount = 99m, type = "credit", description = "obsolete", businessDate = "2026-09-19", version = 1, deleted = false } });
        var summary = await (await client.GetAsync("/summary/daily/2026-09-19")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, obsolete.StatusCode);
        Assert.Equal(25m, summary.GetProperty("credits").GetDecimal());
    }
}
