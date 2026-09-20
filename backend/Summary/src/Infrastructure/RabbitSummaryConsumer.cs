using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public sealed class RabbitSummaryConsumer(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<RabbitSummaryConsumer> logger) : BackgroundService
{
    private IConnection? connection;
    private IModel? channel;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("RabbitMq:Enabled", false)) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try { Consume(stoppingToken); await Task.Delay(Timeout.Infinite, stoppingToken); }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested) { logger.LogWarning(exception, "Summary consumer stopped; retrying."); await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken); }
        }
    }
    private void Consume(CancellationToken token)
    {
        var factory = new ConnectionFactory { Uri = new Uri(configuration["RabbitMq:Uri"] ?? "amqp://guest:guest@rabbitmq:5672/"), DispatchConsumersAsync = true }; connection = factory.CreateConnection(); channel = connection.CreateModel();
        channel.ExchangeDeclare("cashflow.ledger", ExchangeType.Topic, durable: true); channel.QueueDeclare("cashflow.summary", durable: true, exclusive: false, autoDelete: false); channel.QueueBind("cashflow.summary", "cashflow.ledger", "#"); channel.BasicQos(0, 1, false);
        var activeChannel = channel; var consumer = new AsyncEventingBasicConsumer(activeChannel); consumer.Received += async (_, delivery) => { try { var message = JsonSerializer.Deserialize<ProjectionEvent>(Encoding.UTF8.GetString(delivery.Body.ToArray()), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("Invalid ledger event"); using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<ProjectionService>().ApplyAsync(message, token); activeChannel.BasicAck(delivery.DeliveryTag, false); } catch (Exception exception) { logger.LogError(exception, "Ledger event failed"); activeChannel.BasicNack(delivery.DeliveryTag, false, true); } };
        activeChannel.BasicConsume("cashflow.summary", false, consumer);
    }
}
