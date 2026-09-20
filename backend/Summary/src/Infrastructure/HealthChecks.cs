using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

public sealed class SummaryDatabaseReadinessCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<SummaryDbContext>();
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

public static class CashFlowSummaryTelemetry
{
    private static readonly System.Diagnostics.Metrics.Meter Meter = new("CashFlow.Summary", "1.0.0");
    public static readonly System.Diagnostics.Metrics.Counter<long> InboxDuplicates = Meter.CreateCounter<long>("cashflow.inbox.duplicates");
    public static readonly System.Diagnostics.Metrics.Counter<long> ProjectionApplied = Meter.CreateCounter<long>("cashflow.projection.applied");
    public static readonly System.Diagnostics.Metrics.Counter<long> ProjectionFailures = Meter.CreateCounter<long>("cashflow.projection.failures");
    public static readonly System.Diagnostics.Metrics.Counter<long> KeycloakChecks = Meter.CreateCounter<long>("cashflow.keycloak.authorization_checks");
    public static readonly System.Diagnostics.Metrics.Counter<long> KeycloakFailures = Meter.CreateCounter<long>("cashflow.keycloak.authorization_failures");
}
