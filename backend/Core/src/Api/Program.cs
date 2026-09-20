using System.Globalization;
using System.Text;
using System.Text.Json;
using Core.Api;
using Core.Api.Application.Interfaces;
using Core.Api.Application.Models;
using Core.Api.Application.Events;
using Core.Api.Application.Validation;
using Core.Api.Domain.Entities;
using Core.Api.Domain.Events;
using Core.Api.Infrastructure;
using Core.Api.Infrastructure.Identity;
using Core.Api.Infrastructure.Messaging;
using Core.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CashFlow Core API",
        Version = "v1",
        Description = "Lançamentos, auditoria e administração de usuários do CashFlow."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "Informe apenas o token JWT."
    });
    options.OperationFilter<CoreSwaggerExamplesOperationFilter>();
});
var samplingRatio = Math.Clamp(builder.Configuration.GetValue("Telemetry:SamplingRatio", 1.0), 0.0, 1.0);
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter();
});
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "cash-flow-core-api",
            serviceVersion: builder.Configuration["OTEL_SERVICE_VERSION"] ?? "1.0.0").AddAttributes(
            new Dictionary<string, object>
            {
                ["deployment.environment"] =
                    builder.Configuration["OTEL_ENVIRONMENT"] ?? builder.Environment.EnvironmentName,
                ["service.namespace"] = "cashflow",
                ["service.instance.id"] = Environment.MachineName
            }))
    .WithTracing(tracing => tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
        .AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddEntityFrameworkCoreInstrumentation()
        .AddSource("CashFlow.Core.Messaging").AddOtlpExporter())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddMeter("CashFlow.Core")
        .AddOtlpExporter());
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.Authority = builder.Configuration["Keycloak:Authority"];
    o.Audience = builder.Configuration["Keycloak:Audience"] ?? "cashflow";
    o.RequireHttpsMetadata = false;
    o.TokenValidationParameters.ValidIssuer =
        builder.Configuration["Keycloak:Issuer"] ?? builder.Configuration["Keycloak:Authority"];
    var testKey = builder.Configuration["Keycloak:ValidationSigningKey"];
    if (!string.IsNullOrWhiteSpace(testKey))
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(testKey)), ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Keycloak:Issuer"] ?? builder.Configuration["Keycloak:Authority"],
            ValidateAudience = true, ValidAudience = builder.Configuration["Keycloak:Audience"] ?? "cashflow",
            ValidateLifetime = true, ClockSkew = TimeSpan.Zero
        };
});
builder.Services.AddAuthorization();
builder.Services.AddHttpClient("keycloak-current-state");
builder.Services.AddScoped<ICurrentKeycloakAuthorization, CurrentKeycloakAuthorization>();
builder.Services.AddScoped<IKeycloakAdminClient, KeycloakAdminClient>();
builder.Services.AddScoped<LedgerMutationFactory>();
var connection = builder.Configuration.GetConnectionString("Core") ??
                 "Host=postgres;Database=core_db;Username=cashflow;Password=local";
if (builder.Configuration["Database:Provider"] == "Sqlite")
    builder.Services.AddDbContext<CoreDbContext>(o => o.UseSqlite(connection).ConfigureWarnings(w =>
        w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
else builder.Services.AddDbContext<CoreDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddHealthChecks().AddCheck<DatabaseReadinessCheck<CoreDbContext>>("database");
builder.Services.AddHostedService<RabbitOutboxRelay>();
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CashFlow Core API v1"));
if (args.Any(argument => string.Equals(argument, "--migrate", StringComparison.OrdinalIgnoreCase)))
{
    await app.Services.GetRequiredService<CoreDbContext>().Database.MigrateAsync();
    return;
}

var keycloakEnabled = builder.Configuration.GetValue("Keycloak:Enabled", false);
app.UseCors();
if (keycloakEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseCurrentKeycloakAuthorization(builder.Configuration);
}

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Correlation-Id"] =
        ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");
    await next();
});
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "core" }));
app.MapHealthChecks("/readyz");
app.MapControllers();
var createEntry = app.MapPost("/ledger/entries",
    async (CreateEntryRequest request, HttpContext http, CoreDbContext db, LedgerMutationFactory mutations,
        ILogger<Core.Api.Program> logger,
        CancellationToken token) =>
    {
        var errors = EntryValidator.Validate(request);
        var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key)) errors["Idempotency-Key"] = ["required"];
        if (errors.Count > 0) return Results.ValidationProblem(errors, statusCode: 422);
        var actor = Actor(http);
        var date = request.BusinessDate ?? Today();
        var intent =
            $"{request.Amount.ToString(CultureInfo.InvariantCulture)}|{request.Type}|{request.Description}|{date:yyyy-MM-dd}";
        var old = await db.CreationAttempts.Include(x => x.Entry)
            .SingleOrDefaultAsync(x => x.ActorId == actor && x.Key == key, token);
        if (old is not null)
            return old.Intent == intent
                ? Results.Ok(old.Entry)
                : Results.Conflict(new { code = "idempotency_conflict" });
        var entry = new LedgerEntry(Guid.NewGuid(), request.Amount, request.Type!, request.Description!, date, 1,
            false);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        db.LedgerEntries.Add(entry);
        db.CreationAttempts.Add(new CreationAttempt { ActorId = actor, Key = key!, Intent = intent, Entry = entry });
        mutations.Add(db, entry, "created", actor, Correlation(http), "LedgerEntryCreated.v1");
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        logger.LogInformation(
            "Business event {BusinessEvent} published with {EntryId}, {Amount}, {EntryType}, {ActorId}",
            "ledger.entry.created", entry.Id, entry.Amount, entry.Type, actor);
        return Results.Created($"/ledger/entries/{entry.Id}", entry);
    });
var updateEntry = app.MapPut("/ledger/entries/{id:guid}",
    async (Guid id, UpdateEntryRequest request, HttpContext http, CoreDbContext db,
        LedgerMutationFactory mutations, CancellationToken token) =>
    {
        var current = await db.LedgerEntries.SingleOrDefaultAsync(x => x.Id == id, token);
        if (current is null || current.Deleted) return Results.NotFound();
        if (request.Version != current.Version)
            return Results.Conflict(new { code = "stale_version", currentVersion = current.Version });
        var errors = EntryValidator.Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors, statusCode: 422);
        current.Amount = request.Amount;
        current.Type = request.Type!;
        current.Description = request.Description!;
        current.BusinessDate = request.BusinessDate ?? current.BusinessDate;
        current.Version++;
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        mutations.Add(db, current, "updated", Actor(http), Correlation(http), "LedgerEntryUpdated.v1");
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.Ok(current);
    });
var deleteEntry = app.MapDelete("/ledger/entries/{id:guid}",
    async (Guid id, int version, HttpContext http, CoreDbContext db,
        LedgerMutationFactory mutations, CancellationToken token) =>
    {
        var current = await db.LedgerEntries.SingleOrDefaultAsync(x => x.Id == id, token);
        if (current is null || current.Deleted) return Results.NotFound();
        if (version != current.Version)
            return Results.Conflict(new { code = "stale_version", currentVersion = current.Version });
        current.Deleted = true;
        current.Version++;
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        mutations.Add(db, current, "deleted", Actor(http), Correlation(http), "LedgerEntryDeleted.v1");
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return Results.NoContent();
    });
var users = app.MapGet("/identity/users",
    async (HttpContext http, ICurrentKeycloakAuthorization authorization, IKeycloakAdminClient admin,
        CancellationToken token) =>
    {
        if (!await IsAdminAsync(http, authorization, token)) return Results.Forbid();
        return Results.Ok(await admin.ListUsersAsync(token));
    });
var createUser = app.MapPost("/identity/users",
    async (CreateManagedUserRequest request, HttpContext http, ICurrentKeycloakAuthorization authorization,
        IKeycloakAdminClient admin, CancellationToken token) =>
    {
        if (!await IsAdminAsync(http, authorization, token)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(request.Username) ||
            !new[] { "admin", "operator", "auditor" }.Contains(request.Role, StringComparer.Ordinal))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["user"] = ["username and a supported role are required"] });
        var user = await admin.CreateUserAsync(request, token);
        return user is null
            ? Results.Conflict(new { code = "user_exists" })
            : Results.Created($"/identity/users/{user.Id}", user);
    });
var updateUser = app.MapPut("/identity/users/{id}",
    async (string id, UpdateManagedUserRequest request, HttpContext http, ICurrentKeycloakAuthorization authorization,
        IKeycloakAdminClient admin, CancellationToken token) =>
    {
        if (!await IsAdminAsync(http, authorization, token)) return Results.Forbid();
        if (request.Role is not ("admin" or "operator" or "auditor"))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["role"] = ["unsupported role"] });
        try
        {
            return await admin.UpdateUserAsync(id, request, token) ? Results.NoContent() : Results.NotFound();
        }
        catch (LastActiveAdminException)
        {
            return Results.Conflict(new { code = "last_active_admin" });
        }
    });
if (keycloakEnabled)
{
    createEntry.RequireAuthorization();
    updateEntry.RequireAuthorization();
    deleteEntry.RequireAuthorization();
    users.RequireAuthorization();
    createUser.RequireAuthorization();
    updateUser.RequireAuthorization();
}

app.Run();

static async Task<bool> IsAdminAsync(HttpContext http, ICurrentKeycloakAuthorization authorization,
    CancellationToken token)
{
    if (http.User.Identity?.IsAuthenticated != true) return false;
    var state = await authorization.ConfirmAsync(http.User, token);
    return state.Enabled && state.Roles.Contains("admin");
}

static string Actor(HttpContext context) => context.Request.Headers["X-Actor-Id"].FirstOrDefault() ??
                                            context.User.FindFirst("sub")?.Value ?? "demo-operator";

static string Correlation(HttpContext context) =>
    context.Response.Headers["X-Correlation-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString("N");

static DateOnly Today() =>
    DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Sao_Paulo"));

namespace Core.Api
{
    public partial class Program;

}
