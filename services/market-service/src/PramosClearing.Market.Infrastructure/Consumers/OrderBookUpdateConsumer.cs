using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PramosClearing.MarketService.Application.Models;
using PramosClearing.MarketService.Application.Services;
using PramosClearing.MarketService.Infrastructure.Persistence;

namespace PramosClearing.MarketService.Infrastructure.Consumers;

public sealed class OrderBookUpdateConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly KafkaConsumerOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbContextFactory<MarketDataDbContext> _marketDataDbContextFactory;
    private readonly TopOfBookProjector _projector;
    private readonly ILogger<OrderBookUpdateConsumer> _logger;
    private readonly Dictionary<string, Guid> _assetIdsByMarket = new(StringComparer.Ordinal);

    public OrderBookUpdateConsumer(
        IOptions<KafkaConsumerOptions> options,
        IServiceScopeFactory scopeFactory,
        IDbContextFactory<MarketDataDbContext> marketDataDbContextFactory,
        TopOfBookProjector projector,
        ILogger<OrderBookUpdateConsumer> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _marketDataDbContextFactory = marketDataDbContextFactory;
        _projector = projector;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshAssetMapAsync(stoppingToken).ConfigureAwait(false);

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        }).Build();

        consumer.Subscribe(_options.Topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? result;

            try
            {
                result = consumer.Consume(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume failed for topic {Topic}.", _options.Topic);
                continue;
            }

            if (result?.Message?.Value is null)
                continue;

            try
            {
                if (!TryDeserialize(result.Message.Value, out var update))
                {
                    consumer.Commit(result);
                    continue;
                }

                var assetId = await TryResolveAssetIdAsync(update, stoppingToken).ConfigureAwait(false);

                if (assetId is null)
                {
                    _logger.LogWarning(
                        "Skipping order book update for unknown asset {Symbol}@{Exchange}.",
                        update.Symbol,
                        update.Exchange);

                    consumer.Commit(result);
                    continue;
                }

                if (_projector.TryProject(update, assetId.Value, out var tick) && tick is not null)
                    await PersistAsync(tick, stoppingToken).ConfigureAwait(false);

                consumer.Commit(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process Kafka message at {TopicPartitionOffset}.", result.TopicPartitionOffset);
            }
        }

        consumer.Close();
    }

    private static bool TryDeserialize(string payload, out OrderBookUpdateMessage update)
    {
        var message = JsonSerializer.Deserialize<OrderBookUpdateMessage>(payload, JsonOptions);

        if (message is null ||
            string.IsNullOrWhiteSpace(message.Symbol) ||
            string.IsNullOrWhiteSpace(message.Exchange) ||
            string.IsNullOrWhiteSpace(message.Side) ||
            string.IsNullOrWhiteSpace(message.Action))
        {
            update = null!;
            return false;
        }

        update = message;
        return true;
    }

    private async Task<Guid?> TryResolveAssetIdAsync(OrderBookUpdateMessage update, CancellationToken cancellationToken)
    {
        var key = BuildMarketKey(update.Symbol, update.Exchange);

        if (_assetIdsByMarket.TryGetValue(key, out var assetId))
            return assetId;

        await RefreshAssetMapAsync(cancellationToken).ConfigureAwait(false);

        return _assetIdsByMarket.TryGetValue(key, out assetId)
            ? assetId
            : null;
    }

    private async Task RefreshAssetMapAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var marketDbContext = scope.ServiceProvider.GetRequiredService<MarketDbContext>();

        var stocks = await marketDbContext.Stocks
            .AsNoTracking()
            .Where(stock => stock.IsActive)
            .Select(stock => new { stock.Id, stock.Symbol, stock.Exchange })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _assetIdsByMarket.Clear();

        foreach (var stock in stocks)
            _assetIdsByMarket[BuildMarketKey(stock.Symbol, stock.Exchange)] = stock.Id;
    }

    private async Task PersistAsync(PriceTickWriteModel tick, CancellationToken cancellationToken)
    {
        await using var marketDataDbContext = await _marketDataDbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        marketDataDbContext.Add(new PriceTickEntity
        {
            Time = tick.Time,
            AssetId = tick.AssetId,
            Symbol = tick.Symbol,
            Exchange = tick.Exchange,
            Bid = tick.Bid,
            Ask = tick.Ask,
            Last = tick.Last,
            Volume = tick.Volume,
            Source = tick.Source
        });

        await marketDataDbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string BuildMarketKey(string symbol, string exchange) =>
        string.Concat(symbol.ToUpperInvariant(), "@", exchange.ToUpperInvariant());
}