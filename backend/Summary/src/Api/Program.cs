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
builder.AddSummaryTelemetry();
builder.Services.AddSummaryAuthentication(builder.Configuration);
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
