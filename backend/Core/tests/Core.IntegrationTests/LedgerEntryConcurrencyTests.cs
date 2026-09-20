using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Core.IntegrationTests;

public class LedgerEntryConcurrencyTests : IClassFixture<CoreApiFactory>
{
    private readonly HttpClient client;
    private readonly CoreApiFactory factory;
    public LedgerEntryConcurrencyTests(CoreApiFactory factory) { this.factory = factory; client = factory.CreateClient(); }

    [Fact]
    public async Task RejectsStaleVersionWithoutSideEffects()
    {
        var key = Guid.NewGuid().ToString("N");
        using var create = new HttpRequestMessage(HttpMethod.Post, "/ledger/entries") { Content = JsonContent.Create(new { amount = 10m, type = "credit", description = "Concurrency" }) };
        create.Headers.Add("Idempotency-Key", key);
        create.Headers.Add("X-Actor-Id", "concurrency-actor");
        var created = await client.SendAsync(create);
        var entry = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = entry.GetProperty("id").GetGuid();
        var update = await SendAsync(HttpMethod.Put, $"/ledger/entries/{id}", new { amount = 11m, type = "credit", description = "Concurrency", version = 1 }, "update-correlation");
        var beforeStale = await ReadCountsAsync();
        var stale = await SendAsync(HttpMethod.Put, $"/ledger/entries/{id}", new { amount = 12m, type = "credit", description = "Concurrency", version = 1 }, "stale-put-correlation");
        var staleDelete = await client.DeleteAsync($"/ledger/entries/{id}?version=1");
        var afterStale = await ReadCountsAsync();
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleDelete.StatusCode);
        var current = (await (await client.GetAsync("/ledger/entries")).Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal(11m, current.GetProperty("amount").GetDecimal());
        Assert.Equal(2, current.GetProperty("version").GetInt32());
        Assert.Equal(beforeStale, afterStale);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, object payload, string correlation)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(payload) };
        request.Headers.Add("X-Actor-Id", "concurrency-actor");
        request.Headers.Add("X-Correlation-Id", correlation);
        return await client.SendAsync(request);
    }

    private Task<DbCounts> ReadCountsAsync() => factory.ReadDbAsync(async db => new DbCounts(
        await db.LedgerEntries.CountAsync(),
        await db.CreationAttempts.CountAsync(),
        await db.AuditRecords.CountAsync(),
        await db.OutboxEvents.CountAsync()));
}