using Microsoft.EntityFrameworkCore;
using Summary.Api.Infrastructure;
using Summary.Api.Infrastructure.Persistence;

namespace Summary.Api.Configuration;

public static class PersistenceConfiguration
{
    public static IServiceCollection AddSummaryPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Summary")
            ?? "Host=postgres;Database=summary_db;Username=cashflow;Password=local";

        if (configuration["Database:Provider"] == "Sqlite")
        {
            services.AddDbContext<SummaryDbContext>(options =>
                options.UseSqlite(connection)
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(
                            Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
        }
        else
        {
            services.AddDbContext<SummaryDbContext>(options => options.UseNpgsql(connection));
        }

        services.AddHealthChecks()
            .AddCheck<SummaryDatabaseReadinessCheck>("database");

        return services;
    }
}
