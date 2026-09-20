extern alias CoreApi;
using CoreApi::Core.Api;
using CoreApi::Core.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Summary.IntegrationTests;

public sealed class CrossServiceCoreApiFactory(string rabbitUri) : WebApplicationFactory<Program>
{
    private readonly string database = Path.Combine(Path.GetTempPath(), $"cashflow-core-cross-service-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Core", $"Data Source={database}");
        builder.UseSetting("RabbitMq:Enabled", "true");
        builder.UseSetting("RabbitMq:Uri", rabbitUri);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<CoreDbContext>().Database.Migrate();
        return host;
    }

    public async Task<T> ReadDbAsync<T>(Func<CoreDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<CoreDbContext>());
    }
}

public static class TestEventually
{
    public static async Task WaitForAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }

        Assert.True(await condition(), "Expected asynchronous state was not observed before the timeout.");
    }
}