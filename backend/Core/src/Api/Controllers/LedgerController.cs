using Core.Api.Application.Models;
using Core.Api.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Core.Api.Controllers;

[ApiController]
[Route("ledger")]
public sealed class LedgerController(LedgerService service, ILogger<LedgerController> logger) : ControllerBase
{
    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry(CreateEntryRequest request, CancellationToken token)
    {
        var errors = service.Validate(request).ToDictionary(pair => pair.Key, pair => pair.Value);
        var key = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key)) errors["Idempotency-Key"] = ["required"];
        if (errors.Count > 0) return UnprocessableEntity(new ValidationProblemDetails(errors));
        var actor = Actor();
        var date = request.BusinessDate ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo"));
        var old = await service.FindByIdempotencyAsync(actor, key!, token);
        if (old is not null) return Ok(old);
        var entry = new Domain.Entities.LedgerEntry(Guid.NewGuid(), request.Amount, request.Type!, request.Description!, date, 1, false);
        await service.AddAsync(entry, key!, actor, Correlation(), token);
        logger.LogInformation("Business event {BusinessEvent} published with {EntryId}", "ledger.entry.created", entry.Id);
        return Created($"/ledger/entries/{entry.Id}", entry);
    }

    [HttpGet("entries")]
    public async Task<IActionResult> ListEntries([FromQuery] DateOnly? date, CancellationToken token) => Ok(await service.ListAsync(date, token));

    [HttpPut("entries/{id:guid}")]
    public async Task<IActionResult> UpdateEntry(Guid id, UpdateEntryRequest request, CancellationToken token)
    {
        var current = await service.FindAsync(id, token);
        if (current is null || current.Deleted) return NotFound();
        if (request.Version != current.Version) return Conflict(new { code = "stale_version", currentVersion = current.Version });
        var errors = service.Validate(request).ToDictionary(pair => pair.Key, pair => pair.Value);
        if (errors.Count > 0) return UnprocessableEntity(new ValidationProblemDetails(errors));
        current.Amount = request.Amount;
        current.Type = request.Type!;
        current.Description = request.Description!;
        current.BusinessDate = request.BusinessDate ?? current.BusinessDate;
        current.Version++;
        try
        {
            await service.UpdateAsync(current, Actor(), Correlation(), token);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { code = "stale_version" });
        }

        return Ok(current);
    }

    [HttpDelete("entries/{id:guid}")]
    public async Task<IActionResult> DeleteEntry(Guid id, [FromQuery] int version, CancellationToken token)
    {
        var current = await service.FindAsync(id, token);
        if (current is null || current.Deleted) return NotFound();
        if (version != current.Version) return Conflict(new { code = "stale_version", currentVersion = current.Version });
        current.Deleted = true;
        current.Version++;
        try
        {
            await service.DeleteAsync(current, Actor(), Correlation(), token);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { code = "stale_version" });
        }

        return NoContent();
    }

    private string Actor() => Request.Headers["X-Actor-Id"].FirstOrDefault() ?? User.FindFirst("sub")?.Value ?? "demo-operator";
    private string Correlation() => Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
}
