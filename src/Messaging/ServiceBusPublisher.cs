using System.Text.Json;
using Azure.Messaging.ServiceBus;
using IngestionService.Models;

namespace IngestionService.Messaging;

public class ServiceBusPublisher : IAsyncDisposable
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger<ServiceBusPublisher> _logger;

    public ServiceBusPublisher(IConfiguration config, ILogger<ServiceBusPublisher> logger)
    {
        _logger = logger;

        var connectionString = config["ServiceBus:ConnectionString"]
            ?? throw new InvalidOperationException("ServiceBus:ConnectionString não configurado.");
        var topicName = config["ServiceBus:TopicName"] ?? "sensor-readings";

        var client = new ServiceBusClient(connectionString);
        _sender = client.CreateSender(topicName);
    }

    public async Task PublishAsync(SensorReadingEvent evt, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(evt);
        var message = new ServiceBusMessage(body)
        {
            MessageId = evt.ReadingId.ToString(),
            ContentType = "application/json",
            Subject = "SensorReadingEvent",
            ApplicationProperties =
            {
                ["FieldId"] = evt.FieldId.ToString(),
                ["EventType"] = "SensorReadingEvent"
            }
        };

        await _sender.SendMessageAsync(message, ct);
        _logger.LogInformation("Evento publicado no Service Bus. ReadingId={ReadingId} FieldId={FieldId}",
            evt.ReadingId, evt.FieldId);
    }

    public async Task PublishBatchAsync(IEnumerable<SensorReadingEvent> events, CancellationToken ct = default)
    {
        using var batch = await _sender.CreateMessageBatchAsync(ct);

        foreach (var evt in events)
        {
            var body = JsonSerializer.Serialize(evt);
            var message = new ServiceBusMessage(body)
            {
                MessageId = evt.ReadingId.ToString(),
                ContentType = "application/json",
                Subject = "SensorReadingEvent"
            };

            if (!batch.TryAddMessage(message))
            {
                // batch cheio — envia o atual e começa um novo
                await _sender.SendMessagesAsync(batch, ct);
            }
        }

        await _sender.SendMessagesAsync(batch, ct);
        _logger.LogInformation("Batch de {Count} eventos publicado no Service Bus.", events.Count());
    }

    public async ValueTask DisposeAsync() => await _sender.DisposeAsync();
}
