using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
var samplingRatio = Math.Clamp(builder.Configuration.GetValue("Telemetry:SamplingRatio", 1.0), 0.0, 1.0);
builder.Logging.AddOpenTelemetry(logging => { logging.IncludeFormattedMessage = true; logging.IncludeScopes = true; logging.AddOtlpExporter(); });
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "cashflow-summary"))
    .WithTracing(tracing => tracing.SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio))).AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddMeter("CashFlow.Summary").AddOtlpExporter());
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.MapInboundClaims = false; o.Authority = builder.Configuration["Keycloak:Authority"]; o.Audience = builder.Configuration["Keycloak:Audience"] ?? "cashflow"; o.RequireHttpsMetadata = false; o.TokenValidationParameters.ValidIssuer = builder.Configuration["Keycloak:Issuer"] ?? builder.Configuration["Keycloak:Authority"]; var testKey = builder.Configuration["Keycloak:ValidationSigningKey"]; if (!string.IsNullOrWhiteSpace(testKey)) o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(testKey)), ValidateIssuer = true, ValidIssuer = builder.Configuration["Keycloak:Issuer"] ?? builder.Configuration["Keycloak:Authority"], ValidateAudience = true, ValidAudience = builder.Configuration["Keycloak:Audience"] ?? "cashflow", ValidateLifetime = true, ClockSkew = TimeSpan.Zero }; }); builder.Services.AddAuthorization();
builder.Services.AddHttpClient("keycloak-current-state"); builder.Services.AddScoped<ICurrentKeycloakAuthorization, CurrentKeycloakAuthorization>();
var connection = builder.Configuration.GetConnectionString("Summary") ?? "Host=postgres;Database=summary_db;Username=cashflow;Password=local";
if (builder.Configuration["Database:Provider"] == "Sqlite") builder.Services.AddDbContext<SummaryDbContext>(o => o.UseSqlite(connection).ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))); else builder.Services.AddDbContext<SummaryDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddHealthChecks().AddCheck<SummaryDatabaseReadinessCheck>("database");
builder.Services.AddScoped<ProjectionService>(); builder.Services.AddHostedService<RabbitSummaryConsumer>();
var app = builder.Build();
if (args.Any(argument => string.Equals(argument, "--migrate", StringComparison.OrdinalIgnoreCase)))
{
    await app.Services.GetRequiredService<SummaryDbContext>().Database.MigrateAsync();
    return;
}
var keycloakEnabled = builder.Configuration.GetValue("Keycloak:Enabled", false);
app.UseCors(); if (keycloakEnabled) { app.UseAuthentication(); app.UseAuthorization(); app.UseCurrentKeycloakAuthorization(builder.Configuration); }
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "summary" }));
app.MapHealthChecks("/readyz");
app.MapPost("/internal/events", async ([FromBody] ProjectionEvent message, ProjectionService projection, CancellationToken token) => await projection.ApplyAsync(message, token) ? Results.Accepted() : Results.Ok(new { duplicate = true }));
var dailySummary = app.MapGet("/summary/daily/{date}", async (DateOnly date, SummaryDbContext db, IConfiguration config, CancellationToken token) =>
{
    if (!config.GetValue("Summary:Available", true)) return Results.Json(new { freshnessStatus = "unavailable" }, statusCode: 503);
    var summary = await db.DailySummaries.FindAsync([date], token) ?? new DailySummary { Date = date, AsOf = DateTimeOffset.UtcNow };
    var threshold = TimeSpan.FromSeconds(config.GetValue("Summary:FreshnessSeconds", 30)); summary.FreshnessStatus = DateTimeOffset.UtcNow - summary.AsOf > threshold ? "stale" : "current";
    return Results.Ok(summary);
});
if (keycloakEnabled) dailySummary.RequireAuthorization();
app.Run(); public partial class Program;
