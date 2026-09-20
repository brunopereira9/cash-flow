using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Summary.Api.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Summary.Api;
using Summary.Api.Domain.Entities;
using Summary.Api.Domain.Events;
using Summary.Api.Infrastructure;
using Summary.Api.Infrastructure.Identity;
using Summary.Api.Infrastructure.Messaging;
using Summary.Api.Infrastructure.Persistence;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1",
        new OpenApiInfo
        {
            Title = "CashFlow Summary API", Version = "v1", Description = "Projeções e resumos diários do CashFlow."
        });
    options.AddSecurityDefinition("Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
            In = ParameterLocation.Header, Description = "Informe apenas o token JWT."
        });
    options.OperationFilter<SummarySwaggerExamplesOperationFilter>();
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
        .AddService(builder.Configuration["OTEL_SERVICE_NAME"] ?? "cash-flow-summary-api",
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
        .AddSource("CashFlow.Summary.Messaging").AddOtlpExporter())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation()
        .AddMeter("CashFlow.Summary").AddOtlpExporter());
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
builder.Services.AddSummaryPersistence(builder.Configuration);
builder.Services.AddScoped<ProjectionService>();
builder.Services.AddHostedService<RabbitSummaryConsumer>();
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "CashFlow Summary API v1"));
if (args.Any(argument => string.Equals(argument, "--migrate", StringComparison.OrdinalIgnoreCase)))
{
    await app.Services.GetRequiredService<SummaryDbContext>().Database.MigrateAsync();
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

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "summary" }));
app.MapHealthChecks("/readyz");
app.MapControllers();
app.Run();

namespace Summary.Api
{
    public partial class Program;
}
