using Core.Api;
using Core.Api.Application.Events;
using Core.Api.Configuration;
using Core.Api.Infrastructure;
using Core.Api.Infrastructure.Identity;
using Core.Api.Infrastructure.Messaging;
using Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCoreWeb();
builder.AddCoreTelemetry();
builder.Services.AddCoreAuthentication(builder.Configuration);
builder.Services.AddScoped<LedgerMutationFactory>();
builder.Services.AddCorePersistence(builder.Configuration);
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
