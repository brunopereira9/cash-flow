using Summary.Api.Application.Interfaces;
using Summary.Api.Domain.Entities;

namespace Summary.Api.Application.Services;

public sealed class SummaryService(ISummaryRepository repository, IConfiguration configuration)
{
    public async Task<DailySummary> GetDailyAsync(DateOnly date, CancellationToken token)
    {
        var summary = await repository.FindDailyAsync(date, token)
            ?? DailySummary.Create(date, 0, 0, DateTimeOffset.UtcNow);
        var threshold = TimeSpan.FromSeconds(configuration.GetValue("Summary:FreshnessSeconds", 30));
        summary.MarkFreshness(DateTimeOffset.UtcNow - summary.AsOf > threshold ? "stale" : "current");
        return summary;
    }
}
