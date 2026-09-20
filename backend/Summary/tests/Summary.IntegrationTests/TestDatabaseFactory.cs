using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Summary.Api;
using Summary.Api.Infrastructure.Persistence;

namespace Summary.IntegrationTests;

public abstract class MigratedSummaryApiFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<SummaryDbContext>().Database.Migrate();
        return host;
    }
}