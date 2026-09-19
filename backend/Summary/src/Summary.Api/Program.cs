using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.Authority = builder.Configuration["Keycloak:Authority"]; o.Audience = builder.Configuration["Keycloak:Audience"] ?? "cashflow"; o.RequireHttpsMetadata = false; }); builder.Services.AddAuthorization();
var connection = builder.Configuration.GetConnectionString("Summary") ?? "Host=postgres;Database=summary_db;Username=cashflow;Password=local";
if (builder.Configuration["Database:Provider"] == "Sqlite") builder.Services.AddDbContext<SummaryDbContext>(o => o.UseSqlite(connection)); else builder.Services.AddDbContext<SummaryDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddScoped<ProjectionService>(); builder.Services.AddHostedService<RabbitSummaryConsumer>();
var app = builder.Build(); using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<SummaryDbContext>().Database.EnsureCreatedAsync();
app.UseCors(); if (builder.Configuration.GetValue("Keycloak:Enabled", false)) { app.UseAuthentication(); app.UseAuthorization(); }
app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "summary" }));
app.MapPost("/internal/events", async ([FromBody] ProjectionEvent message, ProjectionService projection, CancellationToken token) => await projection.ApplyAsync(message, token) ? Results.Accepted() : Results.Ok(new { duplicate = true }));
app.MapGet("/summary/daily/{date}", async (DateOnly date, SummaryDbContext db, IConfiguration config, CancellationToken token) =>
{
    if (!config.GetValue("Summary:Available", true)) return Results.Json(new { freshnessStatus = "unavailable" }, statusCode: 503);
    var summary = await db.DailySummaries.FindAsync([date], token) ?? new DailySummary { Date = date, AsOf = DateTimeOffset.UtcNow };
    var threshold = TimeSpan.FromSeconds(config.GetValue("Summary:FreshnessSeconds", 30)); summary.FreshnessStatus = DateTimeOffset.UtcNow - summary.AsOf > threshold ? "stale" : "current";
    return Results.Ok(summary);
});
app.Run(); public partial class Program;
