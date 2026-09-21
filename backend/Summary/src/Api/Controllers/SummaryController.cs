using Microsoft.AspNetCore.Mvc;
using Summary.Api.Application.Services;

namespace Summary.Api.Controllers;

[ApiController]
[Route("summary/daily")]
public sealed class SummaryController(
    SummaryService service,
    IConfiguration configuration,
    ILogger<SummaryController> logger) : ControllerBase
{
    [HttpGet("{date}")]
    public async Task<IActionResult> GetDailySummary(DateOnly date, CancellationToken token)
    {
        if (!configuration.GetValue("Summary:Available", true))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { freshnessStatus = "unavailable" });

        var summary = await service.GetDailyAsync(date, token);
        logger.LogInformation(
            "Business event {BusinessEvent} read for {BusinessDate} with freshness {FreshnessStatus}",
            "summary.daily.read", date, summary.FreshnessStatus);
        return Ok(summary);
    }
}
