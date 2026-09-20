using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Summary.Api.Domain.Entities;
using Summary.Api.Infrastructure.Persistence;

namespace Summary.Api.Controllers;

[ApiController]
[Route("summary/daily")]
public sealed class SummaryController(
    SummaryDbContext db,
    IConfiguration configuration,
    ILogger<SummaryController> logger) : ControllerBase
{
    [HttpGet("{date}")]
    public async Task<IActionResult> GetDailySummary(
        DateOnly date,
        CancellationToken token)
    {
        if (!configuration.GetValue("Summary:Available", true))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { freshnessStatus = "unavailable" });
        }

        var summary = await db.DailySummaries.FindAsync([date], token)
            ?? DailySummary.Create(date, 0, 0, DateTimeOffset.UtcNow);
        var threshold = TimeSpan.FromSeconds(
            configuration.GetValue("Summary:FreshnessSeconds", 30));

        summary.MarkFreshness(
            DateTimeOffset.UtcNow - summary.AsOf > threshold
                ? "stale"
                : "current");

        logger.LogInformation(
            "Business event {BusinessEvent} read for {BusinessDate} with freshness {FreshnessStatus}",
            "summary.daily.read",
            date,
            summary.FreshnessStatus);

        return Ok(summary);
    }
}
