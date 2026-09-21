using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Summary.IntegrationTests;

[Collection("RabbitMQ integration")]
public sealed class SummaryRecoveryTests
{
    private readonly RabbitMqFixture rabbit;

    public SummaryRecoveryTests(RabbitMqFixture rabbit) => this.rabbit = rabbit;

    [Fact]
    public async Task CoreAcceptsEntryWhileSummaryIsUnavailableAndSummaryRecoversFromEvent()
    {
        var database = Path.Combine(Path.GetTempPath(), $"cashflow-summary-recovery-{Guid.NewGuid():N}.db");
        using var unavailableSummary = new SummaryApiFactory { Available = false, DatabasePath = database };
        using var unavailableClient = unavailableSummary.CreateClient();
        using var core = new CrossServiceCoreApiFactory(rabbit.Uri);
        using var coreClient = core.CreateClient();
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable,
            (await unavailableClient.GetAsync($"/summary/daily/{date:yyyy-MM-dd}")).StatusCode);

        using var create = new HttpRequestMessage(HttpMethod.Post, "/ledger/entries")
        {
            Content = JsonContent.Create(new { amount = 42m, type = "credit", description = "recovery", businessDate = date })
        };
        create.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var created = await coreClient.SendAsync(create);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var recoveredSummary = new SummaryApiFactory { RabbitUri = rabbit.Uri, DatabasePath = database };
        using var recoveredClient = recoveredSummary.CreateClient();
        await TestEventually.WaitForAsync(async () =>
        {
            var response = await recoveredClient.GetAsync($"/summary/daily/{date:yyyy-MM-dd}");
            if (response.StatusCode != HttpStatusCode.OK) return false;
            var summary = await response.Content.ReadFromJsonAsync<JsonElement>();
            return summary.GetProperty("credits").GetDecimal() == 42m &&
                   summary.GetProperty("freshnessStatus").GetString() == "current";
        }, TimeSpan.FromSeconds(30));
    }
}
