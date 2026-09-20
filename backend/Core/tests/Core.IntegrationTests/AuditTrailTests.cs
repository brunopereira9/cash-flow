using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public class AuditTrailTests : IClassFixture<CoreApiFactory>
{
    private readonly HttpClient client;
    private readonly CoreApiFactory factory;
    public AuditTrailTests(CoreApiFactory factory) { this.factory = factory; client = factory.CreateClient(); }

    [Fact]
    public async Task PersistsBusinessMutationWithoutSecrets()
    {
        var idempotency = Guid.NewGuid().ToString("N");
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/ledger/entries") { Content = JsonContent.Create(new { amount = 10m, type = "credit", description = "Auditable" }) };
        createRequest.Headers.Add("Idempotency-Key", idempotency);
        createRequest.Headers.Add("X-Actor-Id", "actor-1");
        createRequest.Headers.Add("X-Correlation-Id", "correlation-create");
        var created = await client.SendAsync(createRequest);
        var entry = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = entry.GetProperty("id").GetGuid();
        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/ledger/entries/{id}") { Content = JsonContent.Create(new { amount = 12m, type = "debit", description = "Auditable edit", version = 1 }) };
        updateRequest.Headers.Add("X-Actor-Id", "actor-2");
        updateRequest.Headers.Add("X-Correlation-Id", "correlation-update");
        var updated = await client.SendAsync(updateRequest);
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/ledger/entries/{id}?version=2");
        deleteRequest.Headers.Add("X-Actor-Id", "actor-3");
        deleteRequest.Headers.Add("X-Correlation-Id", "correlation-delete");
        var deleted = await client.SendAsync(deleteRequest);
        var auditResponse = await client.GetAsync("/audit");
        Assert.True(auditResponse.IsSuccessStatusCode, await auditResponse.Content.ReadAsStringAsync());
        var audit = await auditResponse.Content.ReadFromJsonAsync<JsonElement>();
        var records = audit.EnumerateArray().Where(x => x.GetProperty("entryId").GetGuid() == id).ToList();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(3, records.Count);
        Assert.Equal(new[] { "created", "deleted", "updated" }, records.Select(x => x.GetProperty("action").GetString()).OrderBy(x => x).ToArray());
        Assert.Contains(records, x => x.GetProperty("actorId").GetString() == "actor-1" && x.GetProperty("correlationId").GetString() == "correlation-create");
        Assert.Contains(records, x => x.GetProperty("actorId").GetString() == "actor-2" && x.GetProperty("correlationId").GetString() == "correlation-update");
        Assert.Contains(records, x => x.GetProperty("actorId").GetString() == "actor-3" && x.GetProperty("correlationId").GetString() == "correlation-delete");
        Assert.All(records, record => {
            Assert.DoesNotContain("token", record.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", record.ToString(), StringComparison.OrdinalIgnoreCase);
        });
        Assert.Equal(3, await factory.ReadDbAsync(db => db.AuditRecords.CountAsync(x => x.EntryId == id)));
    }
}
