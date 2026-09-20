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
if (keycloakEnabled)
{
}

app.Run();

namespace Core.Api
{
    public partial class Program;

}
