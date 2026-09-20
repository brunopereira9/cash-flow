using System.Text;
using Core.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;

namespace Core.Api.Infrastructure.Messaging;

public sealed class RabbitOutboxRelay(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<RabbitOutboxRelay> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("RabbitMq:Enabled", false)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RelayBatch(stoppingToken);
            }
            catch (Exception exception)
            {
                CashFlowCoreTelemetry.OutboxFailures.Add(1);
                logger.LogWarning(exception, "Outbox relay unavailable; pending events remain retryable.");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task RelayBatch(CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        if ((await db.Database.GetPendingMigrationsAsync(token)).Any())
            return;

        var pending = (await db.OutboxEvents.Where(x => x.PublishedAt == null).ToListAsync(token))
            .OrderBy(x => x.OccurredAt).Take(50).ToList();
        if (pending.Count == 0) return;
        var factory = new ConnectionFactory
        { Uri = new Uri(configuration["RabbitMq:Uri"] ?? "amqp://guest:guest@rabbitmq:5672/") };
        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();
        channel.ExchangeDeclare("cashflow.ledger", ExchangeType.Topic, durable: true);
        channel.ConfirmSelect();
        foreach (var message in pending)
        {
            using var activity = CashFlowCoreTelemetry.Messaging.StartActivity("cashflow.ledger publish",
                System.Diagnostics.ActivityKind.Producer);
            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination.name", "cashflow.ledger");
            activity?.SetTag("messaging.operation.type", "publish");
            activity?.SetTag("messaging.rabbitmq.destination.routing_key", message.Name);
            activity?.SetTag("messaging.message.id", message.EventId.ToString());
            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.MessageId = message.EventId.ToString();
            properties.Headers = new Dictionary<string, object>();
            Propagators.DefaultTextMapPropagator.Inject(
                new PropagationContext(activity?.Context ?? System.Diagnostics.Activity.Current?.Context ?? default,
                    Baggage.Current), properties.Headers,
                static (headers, key, value) => headers[key] = Encoding.UTF8.GetBytes(value));
            channel.BasicPublish("cashflow.ledger", message.Name, properties, Encoding.UTF8.GetBytes(message.Payload));
            channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
            message.PublishedAt = DateTimeOffset.UtcNow;
            CashFlowCoreTelemetry.OutboxPublished.Add(1);
            logger.LogInformation("Business event {BusinessEvent} published to {Destination} with {EventId}",
                "ledger.outbox.published", "cashflow.ledger", message.EventId);
        }

        await db.SaveChangesAsync(token);
    }
}
