using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PramosClearing.OrderBook.Application.Ports;
using PramosClearing.OrderBook.Domain.Events;

namespace PramosClearing.OrderBook.Infrastructure.Publishers;

public sealed class KafkaOrderBookEventPublisher : IOrderBookEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;
    private readonly ILogger<KafkaOrderBookEventPublisher> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters           = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public KafkaOrderBookEventPublisher(
        IOptions<KafkaOptions> options,
        ILogger<KafkaOrderBookEventPublisher> logger)
    {
        var cfg = options.Value;
        _topic  = cfg.Topic;
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers  = cfg.BootstrapServers,
            Acks              = Acks.Leader,
            MessageSendMaxRetries = cfg.RetryCount,
            RetryBackoffMs    = cfg.RetryBackoffMs
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();

        _logger.LogInformation(
            "Kafka producer initialised. BootstrapServers={Servers}, Topic={Topic}",
            cfg.BootstrapServers,
            _topic);
    }

    public async Task PublishAsync(OrderBookUpdate update, CancellationToken ct)
    {
        var key     = $"{update.Symbol}@{update.Exchange}";
        var payload = JsonSerializer.Serialize(update, JsonOptions);

        try
        {
            await _producer
                .ProduceAsync(_topic, new Message<string, string> { Key = key, Value = payload }, ct)
                .ConfigureAwait(false);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish to topic {Topic} for key {Key}: {Reason}",
                _topic,
                key,
                ex.Error.Reason);
        }
    }

    public void Dispose() => _producer.Dispose();
}
