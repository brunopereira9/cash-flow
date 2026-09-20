using Core.Api.Application.Events;
using Core.Api.Application.Models;
using Core.Api.Application.Validation;
using Core.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Controllers;

[ApiController]
[Route("ledger")]
public sealed class LedgerController(
    CoreDbContext db,
    LedgerMutationFactory mutations,
    ILogger<LedgerController> logger) : ControllerBase
{
    [HttpPost("entries")]
    public async Task<IActionResult> CreateEntry(
        CreateEntryRequest request,
        CancellationToken token)
    {
        var errors = EntryValidator.Validate(request);
        var key = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
            errors["Idempotency-Key"] = ["required"];
        if (errors.Count > 0)
            return UnprocessableEntity(new ValidationProblemDetails(errors));

        var actor = Request.Headers["X-Actor-Id"].FirstOrDefault()
            ?? User.FindFirst("sub")?.Value
            ?? "demo-operator";
        var date = request.BusinessDate ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo"));
        var intent = $"{request.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}|{request.Type}|{request.Description}|{date:yyyy-MM-dd}";
        var old = await db.CreationAttempts.Include(x => x.Entry).SingleOrDefaultAsync(x => x.ActorId == actor && x.Key == key, token);
        if (old is not null)
            return old.Intent == intent ? Ok(old.Entry) : Conflict(new { code = "idempotency_conflict" });

        var entry = new Domain.Entities.LedgerEntry(Guid.NewGuid(), request.Amount, request.Type!, request.Description!, date, 1, false);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        db.LedgerEntries.Add(entry);
        db.CreationAttempts.Add(new Domain.Entities.CreationAttempt { ActorId = actor, Key = key!, Intent = intent, Entry = entry });
        mutations.Add(db, entry, "created", actor, Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N"), "LedgerEntryCreated.v1");
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        logger.LogInformation("Business event {BusinessEvent} published with {EntryId}", "ledger.entry.created", entry.Id);
        return Created($"/ledger/entries/{entry.Id}", entry);
    }
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
