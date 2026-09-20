namespace CashFlow.Core.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class DatabaseReadinessCheck<TContext>(IServiceScopeFactory scopes) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<TContext>();
            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("database is not reachable");
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("database is not reachable");
        }
    }
}

public static class CashFlowCoreTelemetry
{
    public static readonly System.Diagnostics.ActivitySource Messaging = new("CashFlow.Core.Messaging");
    private static readonly System.Diagnostics.Metrics.Meter Meter = new("CashFlow.Core", "1.0.0");
    public static readonly System.Diagnostics.Metrics.Counter<long> OutboxPublished = Meter.CreateCounter<long>("cashflow.outbox.published");
    public static readonly System.Diagnostics.Metrics.Counter<long> OutboxFailures = Meter.CreateCounter<long>("cashflow.outbox.failures");
    public static readonly System.Diagnostics.Metrics.Counter<long> KeycloakChecks = Meter.CreateCounter<long>("cashflow.keycloak.authorization_checks");
    public static readonly System.Diagnostics.Metrics.Counter<long> KeycloakFailures = Meter.CreateCounter<long>("cashflow.keycloak.authorization_failures");
}
