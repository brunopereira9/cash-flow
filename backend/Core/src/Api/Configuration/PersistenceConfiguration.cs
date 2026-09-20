using Core.Api.Infrastructure;
using Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Core.Api.Configuration;

public static class PersistenceConfiguration
{
    public static IServiceCollection AddCorePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Core")
            ?? "Host=postgres;Database=core_db;Username=cashflow;Password=local";

        if (configuration["Database:Provider"] == "Sqlite")
        {
            services.AddDbContext<CoreDbContext>(options =>
                options.UseSqlite(connection)
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(
                            Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));
        }
        else
        {
            services.AddDbContext<CoreDbContext>(options => options.UseNpgsql(connection));
        }

        services.AddHealthChecks()
            .AddCheck<DatabaseReadinessCheck<CoreDbContext>>("database");

        return services;
    }
}
