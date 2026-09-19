using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

public sealed class CoreApiFactory : WebApplicationFactory<Program>
{
    private readonly string database = Path.Combine(Path.GetTempPath(), $"cashflow-core-{Guid.NewGuid():N}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Core", $"Data Source={database}");
        builder.UseSetting("RabbitMq:Enabled", "false");
    }
}

public class LedgerEntryCreationTests : IClassFixture<CoreApiFactory>
{
    private readonly HttpClient client;
    public LedgerEntryCreationTests(CoreApiFactory factory) => client = factory.CreateClient();

    [Fact]
    public async Task AcceptsValidEntryAndDefaultsBusinessDate()
    {
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var response = await client.PostAsJsonAsync("/ledger/entries", new { amount = 10.00m, type = "credit", description = "Salary" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var entry = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("credit", entry.GetProperty("type").GetString());
        Assert.Equal(DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo")), DateOnly.Parse(entry.GetProperty("businessDate").GetString()!));
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/ledger/entries", new { amount = 1.234m, type = "credit", description = "bad" })).StatusCode);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync("/ledger/entries", new { amount = 2m, type = "credit", description = "future", businessDate = "2099-01-01" })).StatusCode);
    }

    [Fact]
    public async Task RejectsInvalidEntry()
    {
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var response = await client.PostAsJsonAsync("/ledger/entries", new { amount = -1m, type = "", description = "" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task IsIdempotentByActorAndAttemptKey()
    {
        var key = Guid.NewGuid().ToString("N");
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var request = new { amount = 12.50m, type = "debit", description = "Rent" };
        var first = await client.PostAsJsonAsync("/ledger/entries", request);
        var second = await client.PostAsJsonAsync("/ledger/entries", request);
        var changed = await client.PostAsJsonAsync("/ledger/entries", new { amount = 13m, type = "debit", description = "Rent" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(), (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }
}
