using Core.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Controllers;

[ApiController]
[Route("audit")]
public sealed class AuditController(CoreDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? actor,
        [FromQuery] string? operation,
        [FromQuery] string? correlation,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken token)
    {
        var query = db.AuditRecords.AsQueryable();

        if (!string.IsNullOrWhiteSpace(actor))
            query = query.Where(record => record.ActorId == actor);

        if (!string.IsNullOrWhiteSpace(operation))
            query = query.Where(record => record.Action == operation);

        if (!string.IsNullOrWhiteSpace(correlation))
            query = query.Where(record => record.CorrelationId == correlation);

        if (from is not null)
            query = query.Where(record => record.At >= from);

        if (to is not null)
            query = query.Where(record => record.At <= to);

        var records = await query.ToListAsync(token);
        return Ok(records.OrderByDescending(record => record.At));
    }
}
