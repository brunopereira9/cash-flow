using Summary.Api.Configuration;
using Microsoft.EntityFrameworkCore;
using Summary.Api;
using Summary.Api.Infrastructure;
using Summary.Api.Infrastructure.Identity;
using Summary.Api.Infrastructure.Messaging;
using Summary.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSummaryWeb();
builder.AddSummaryTelemetry();
builder.Services.AddSummaryAuthentication(builder.Configuration);
builder.Services.AddSummaryPersistence(builder.Configuration);
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
