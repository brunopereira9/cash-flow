using Core.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Controllers;

[ApiController]
[Route("ledger")]
public sealed class LedgerController(CoreDbContext db) : ControllerBase
{
    [HttpGet("entries")]
    public async Task<IActionResult> ListEntries(
        [FromQuery] DateOnly? date,
        CancellationToken token)
    {
        var query = db.LedgerEntries
            .Where(entry => !entry.Deleted);

        if (date.HasValue)
        {
            query = query.Where(entry => entry.BusinessDate == date.Value);
        }

        var entries = await query
            .OrderBy(entry => entry.BusinessDate)
            .ThenBy(entry => entry.Id)
            .ToListAsync(token);

        return Ok(entries);
    }
}
