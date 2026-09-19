using System.Collections.Concurrent;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<LedgerStore>();
var app = builder.Build();
string Correlation(HttpContext context) => context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Correlation-Id"] = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    await next();
});

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "core" }));

app.MapPost("/ledger/entries", (CreateEntryRequest request, HttpContext http, LedgerStore store) =>
{
    var actor = http.Request.Headers["X-Actor-Id"].FirstOrDefault() ?? "demo-operator";
    var attemptKey = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(attemptKey)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["Idempotency-Key"] = ["required"] });

    var validation = EntryValidation.Validate(request);
    if (validation.Count > 0) return Results.ValidationProblem(validation);
    var businessDate = request.BusinessDate ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo"));
    var intent = $"{request.Amount.ToString(CultureInfo.InvariantCulture)}|{request.Type}|{request.Description}|{businessDate:yyyy-MM-dd}";
    var existing = store.FindAttempt(actor, attemptKey);
    if (existing is not null)
    {
        if (existing.Intent != intent) return Results.Conflict(new { code = "idempotency_conflict" });
        return Results.Ok(existing.Entry);
    }

    var entry = new LedgerEntry(Guid.NewGuid(), request.Amount, request.Type!, request.Description!, businessDate, 1, false);
    store.Add(entry, actor, attemptKey, intent, Correlation(http));
    return Results.Created($"/ledger/entries/{entry.Id}", entry);
});

app.MapGet("/ledger/entries", (LedgerStore store) => Results.Ok(store.Entries.Where(x => !x.Deleted)));

app.MapPut("/ledger/entries/{id:guid}", (Guid id, UpdateEntryRequest request, HttpContext http, LedgerStore store) =>
{
    var current = store.Get(id);
    if (current is null || current.Deleted) return Results.NotFound();
    if (request.Version != current.Version) return Results.Conflict(new { code = "stale_version", currentVersion = current.Version });
    var errors = EntryValidation.Validate(request);
    if (errors.Count > 0) return Results.ValidationProblem(errors);
    var updated = current with { Amount = request.Amount, Type = request.Type!, Description = request.Description!, BusinessDate = request.BusinessDate ?? current.BusinessDate, Version = current.Version + 1 };
    store.Update(updated, http.Request.Headers["X-Actor-Id"].FirstOrDefault() ?? "demo-operator", Correlation(http));
    return Results.Ok(updated);
});

app.MapDelete("/ledger/entries/{id:guid}", (Guid id, int version, HttpContext http, LedgerStore store) =>
{
    var current = store.Get(id);
    if (current is null || current.Deleted) return Results.NotFound();
    if (version != current.Version) return Results.Conflict(new { code = "stale_version", currentVersion = current.Version });
    store.Update(current with { Deleted = true, Version = current.Version + 1 }, http.Request.Headers["X-Actor-Id"].FirstOrDefault() ?? "demo-operator", Correlation(http));
    return Results.NoContent();
});

app.MapGet("/audit", (LedgerStore store) => Results.Ok(store.Audit));

app.Run();

public partial class Program;

record CreateEntryRequest(decimal Amount, string? Type, string? Description, DateOnly? BusinessDate);
record UpdateEntryRequest(decimal Amount, string? Type, string? Description, DateOnly? BusinessDate, int Version);
record LedgerEntry(Guid Id, decimal Amount, string Type, string Description, DateOnly BusinessDate, int Version, bool Deleted);
record AuditRecord(Guid EntryId, string Action, string ActorId, string CorrelationId, DateTimeOffset At);
record CreationAttempt(string ActorId, string Key, string Intent, LedgerEntry Entry);

static partial class EntryValidation
{
    public static Dictionary<string, string[]> Validate(dynamic request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount) errors["Amount"] = ["must be positive with at most two decimal places"];
        if (string.IsNullOrWhiteSpace(request.Type)) errors["Type"] = ["required"];
        if (string.IsNullOrWhiteSpace(request.Description)) errors["Description"] = ["required"];
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo"));
        if (request.BusinessDate is DateOnly date && date > today) errors["BusinessDate"] = ["cannot be in the future"];
        return errors;
    }
}

sealed class LedgerStore
{
    private readonly ConcurrentDictionary<Guid, LedgerEntry> entries = new();
    private readonly ConcurrentDictionary<string, CreationAttempt> attempts = new();
    public List<AuditRecord> Audit { get; } = [];
    public List<object> Outbox { get; } = [];
    public IEnumerable<LedgerEntry> Entries => entries.Values.OrderBy(x => x.BusinessDate).ThenBy(x => x.Id);
    public LedgerEntry? Get(Guid id) => entries.TryGetValue(id, out var value) ? value : null;
    public CreationAttempt? FindAttempt(string actor, string key) => attempts.TryGetValue($"{actor}:{key}", out var value) ? value : null;
    public void Add(LedgerEntry entry, string actor, string key, string intent, string correlation)
    {
        entries[entry.Id] = entry; attempts[$"{actor}:{key}"] = new(actor, key, intent, entry);
        Audit.Add(new(entry.Id, "created", actor, correlation, DateTimeOffset.UtcNow)); Outbox.Add(new { EventId = Guid.NewGuid(), Name = "LedgerEntryCreated.v1", Entry = entry });
    }
    public void Update(LedgerEntry entry, string actor, string correlation)
    {
        entries[entry.Id] = entry; var action = entry.Deleted ? "deleted" : "updated";
        Audit.Add(new(entry.Id, action, actor, correlation, DateTimeOffset.UtcNow)); Outbox.Add(new { EventId = Guid.NewGuid(), Name = $"LedgerEntry{(entry.Deleted ? "Deleted" : "Updated")}.v1", Entry = entry });
    }
}
