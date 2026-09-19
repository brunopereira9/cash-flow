using System.Text;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

public sealed class RabbitOutboxRelay(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<RabbitOutboxRelay> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("RabbitMq:Enabled", false)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RelayBatch(stoppingToken); } catch (Exception exception) { logger.LogWarning(exception, "Outbox relay failed; events remain pending for retry."); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
    private async Task RelayBatch(CancellationToken token)
    {
        using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var pending = await db.OutboxEvents.Where(x => x.PublishedAt == null).OrderBy(x => x.OccurredAt).Take(50).ToListAsync(token); if (pending.Count == 0) return;
        var factory = new ConnectionFactory { Uri = new Uri(configuration["RabbitMq:Uri"] ?? "amqp://guest:guest@rabbitmq:5672/") };
        using var connection = factory.CreateConnection(); using var channel = connection.CreateModel();
        channel.ExchangeDeclare("cashflow.ledger", ExchangeType.Topic, durable: true); channel.ConfirmSelect();
        foreach (var message in pending) { var properties = channel.CreateBasicProperties(); properties.Persistent = true; properties.MessageId = message.EventId.ToString(); channel.BasicPublish("cashflow.ledger", message.Name, properties, Encoding.UTF8.GetBytes(message.Payload)); channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5)); message.PublishedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync(token);
    }
}
