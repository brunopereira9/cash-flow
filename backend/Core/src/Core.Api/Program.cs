using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.Authority = builder.Configuration["Keycloak:Authority"]; o.Audience = builder.Configuration["Keycloak:Audience"] ?? "cashflow"; o.RequireHttpsMetadata = false; });
builder.Services.AddAuthorization();
var connection = builder.Configuration.GetConnectionString("Core") ?? "Host=postgres;Database=core_db;Username=cashflow;Password=local";
if (builder.Configuration["Database:Provider"] == "Sqlite") builder.Services.AddDbContext<CoreDbContext>(o => o.UseSqlite(connection)); else builder.Services.AddDbContext<CoreDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddHostedService<RabbitOutboxRelay>();
var app = builder.Build();
using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<CoreDbContext>().Database.EnsureCreatedAsync();
app.UseCors(); if (builder.Configuration.GetValue("Keycloak:Enabled", false)) { app.UseAuthentication(); app.UseAuthorization(); }
app.Use(async (ctx, next) => { ctx.Response.Headers["X-Correlation-Id"] = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N"); await next(); });
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "core" }));
if (builder.Configuration.GetValue("Keycloak:Enabled", false)) app.MapMethods("/ledger/{**path}", ["GET", "POST", "PUT", "DELETE"], () => Results.Unauthorized()).RequireAuthorization();
app.MapPost("/ledger/entries", async (CreateEntryRequest request, HttpContext http, CoreDbContext db, CancellationToken token) =>
{
    var errors = EntryValidation.Validate(request); var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault(); if (string.IsNullOrWhiteSpace(key)) errors["Idempotency-Key"] = ["required"]; if (errors.Count > 0) return Results.ValidationProblem(errors, statusCode: 422);
    var actor = Actor(http); var date = request.BusinessDate ?? Today(); var intent = $"{request.Amount.ToString(CultureInfo.InvariantCulture)}|{request.Type}|{request.Description}|{date:yyyy-MM-dd}";
    var old = await db.CreationAttempts.Include(x => x.Entry).SingleOrDefaultAsync(x => x.ActorId == actor && x.Key == key, token); if (old is not null) return old.Intent == intent ? Results.Ok(old.Entry) : Results.Conflict(new { code = "idempotency_conflict" });
    var entry = new LedgerEntry(Guid.NewGuid(), request.Amount, request.Type!, request.Description!, date, 1, false);
    await using var transaction = await db.Database.BeginTransactionAsync(token); db.LedgerEntries.Add(entry); db.CreationAttempts.Add(new CreationAttempt { ActorId = actor, Key = key!, Intent = intent, Entry = entry }); AddMutation(db, entry, "created", actor, Correlation(http), "LedgerEntryCreated.v1"); await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
    return Results.Created($"/ledger/entries/{entry.Id}", entry);
});
app.MapGet("/ledger/entries", async (CoreDbContext db, CancellationToken token) => Results.Ok(await db.LedgerEntries.Where(x => !x.Deleted).OrderBy(x => x.BusinessDate).ThenBy(x => x.Id).ToListAsync(token)));
app.MapPut("/ledger/entries/{id:guid}", async (Guid id, UpdateEntryRequest request, HttpContext http, CoreDbContext db, CancellationToken token) =>
{
    var current = await db.LedgerEntries.SingleOrDefaultAsync(x => x.Id == id, token); if (current is null || current.Deleted) return Results.NotFound(); if (request.Version != current.Version) return Results.Conflict(new { code = "stale_version", currentVersion = current.Version }); var errors = EntryValidation.Validate(request); if (errors.Count > 0) return Results.ValidationProblem(errors, statusCode: 422);
    current.Amount = request.Amount; current.Type = request.Type!; current.Description = request.Description!; current.BusinessDate = request.BusinessDate ?? current.BusinessDate; current.Version++;
    await using var transaction = await db.Database.BeginTransactionAsync(token); AddMutation(db, current, "updated", Actor(http), Correlation(http), "LedgerEntryUpdated.v1"); await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return Results.Ok(current);
});
app.MapDelete("/ledger/entries/{id:guid}", async (Guid id, int version, HttpContext http, CoreDbContext db, CancellationToken token) =>
{
    var current = await db.LedgerEntries.SingleOrDefaultAsync(x => x.Id == id, token); if (current is null || current.Deleted) return Results.NotFound(); if (version != current.Version) return Results.Conflict(new { code = "stale_version", currentVersion = current.Version }); current.Deleted = true; current.Version++;
    await using var transaction = await db.Database.BeginTransactionAsync(token); AddMutation(db, current, "deleted", Actor(http), Correlation(http), "LedgerEntryDeleted.v1"); await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return Results.NoContent();
});
app.MapGet("/audit", async (CoreDbContext db, CancellationToken token) => Results.Ok((await db.AuditRecords.ToListAsync(token)).OrderBy(x => x.At)));
app.Run();
static void AddMutation(CoreDbContext db, LedgerEntry entry, string action, string actor, string correlation, string name)
{ db.AuditRecords.Add(new AuditRecord { EntryId = entry.Id, Action = action, ActorId = actor, CorrelationId = correlation }); var eventId = Guid.NewGuid(); var payload = JsonSerializer.Serialize(new LedgerEventEnvelope(eventId, name, entry.Version, new(entry.Id, entry.Amount, entry.Type, entry.Description, entry.BusinessDate, entry.Version, entry.Deleted))); db.OutboxEvents.Add(new OutboxEvent { EventId = eventId, Name = name, Version = entry.Version, Payload = payload }); }
static string Actor(HttpContext context) => context.Request.Headers["X-Actor-Id"].FirstOrDefault() ?? "demo-operator";
static string Correlation(HttpContext context) => context.Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo"));
public partial class Program;
public record CreateEntryRequest(decimal Amount, string? Type, string? Description, DateOnly? BusinessDate);
public record UpdateEntryRequest(decimal Amount, string? Type, string? Description, DateOnly? BusinessDate, int Version);
public static class EntryValidation { public static Dictionary<string, string[]> Validate(dynamic request) { var errors = new Dictionary<string, string[]>(); if (request.Amount <= 0 || decimal.Round(request.Amount, 2) != request.Amount) errors["Amount"] = ["must be positive with at most two decimal places"]; if (string.IsNullOrWhiteSpace(request.Type)) errors["Type"] = ["required"]; if (string.IsNullOrWhiteSpace(request.Description)) errors["Description"] = ["required"]; if (request.BusinessDate is DateOnly date && date > Today()) errors["BusinessDate"] = ["cannot be in the future"]; return errors; } private static DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo")); }
