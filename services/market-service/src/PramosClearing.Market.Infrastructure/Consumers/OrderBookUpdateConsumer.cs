using System.Data;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
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
            var batch = DrainBatch(consumer, _options.BatchSize, _options.FlushIntervalMs, stoppingToken);

            if (batch.Count == 0)
                continue;

            var ticks = new List<PriceTickEntity>(batch.Count);

            foreach (var result in batch)
            {
                if (!TryDeserialize(result.Message.Value, out var update))
                    continue;

                var assetId = await TryResolveAssetIdAsync(update, stoppingToken).ConfigureAwait(false);

                if (assetId is null)
                {
                    _logger.LogWarning(
                        "Skipping order book update for unknown asset {Symbol}@{Exchange}.",
                        update.Symbol,
                        update.Exchange);
                    continue;
                }

                if (_projector.TryProject(update, assetId.Value, out var tick) && tick is not null)
                {
                    ticks.Add(new PriceTickEntity
                    {
                        Time     = tick.Time,
                        AssetId  = tick.AssetId,
                        Symbol   = tick.Symbol,
                        Exchange = tick.Exchange,
                        Bid      = tick.Bid,
                        Ask      = tick.Ask,
                        Last     = tick.Last,
                        Volume   = tick.Volume,
                        Source   = tick.Source
                    });
                }
            }

            try
            {
                if (ticks.Count > 0)
                    await BulkPersistAsync(ticks, stoppingToken).ConfigureAwait(false);

                consumer.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist batch of {Count} ticks.", ticks.Count);
            }
        }

        consumer.Close();
    }

    private static List<ConsumeResult<string, string>> DrainBatch(
        IConsumer<string, string> consumer,
        int maxSize,
        int maxWaitMs,
        CancellationToken ct)
    {
        var batch = new List<ConsumeResult<string, string>>(maxSize);
        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);

        while (batch.Count < maxSize && !ct.IsCancellationRequested)
        {
            var remaining = (int)Math.Ceiling((deadline - DateTime.UtcNow).TotalMilliseconds);
            if (remaining <= 0)
                break;

            ConsumeResult<string, string>? result;
            try
            {
                result = consumer.Consume(TimeSpan.FromMilliseconds(remaining));
            }
            catch (ConsumeException ex) when (!ct.IsCancellationRequested)
            {
                _ = ex;
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (result is null)
                break;

            if (result.Message?.Value is not null)
                batch.Add(result);
        }

        return batch;
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

    private async Task BulkPersistAsync(IReadOnlyList<PriceTickEntity> ticks, CancellationToken cancellationToken)
    {
        await using var context = await _marketDataDbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Deduplicate by the new PK (time, asset_id). Multiple messages in the
        // same batch window can project to the same instant for the same asset.
        var unique = new Dictionary<(DateTime, Guid), PriceTickEntity>(ticks.Count);
        foreach (var tick in ticks)
            unique[(tick.Time, tick.AssetId)] = tick;

        var conn = (NpgsqlConnection)context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Ensure the staging table exists for this connection session; clear it.
        // No indexes on the staging table = COPY runs at maximum speed.
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TEMP TABLE IF NOT EXISTS _stage_price_ticks (
                    time        TIMESTAMPTZ    NOT NULL,
                    asset_id    UUID           NOT NULL,
                    symbol      TEXT           NOT NULL,
                    exchange    TEXT           NOT NULL,
                    bid         NUMERIC(28, 8) NOT NULL,
                    ask         NUMERIC(28, 8) NOT NULL,
                    last        NUMERIC(28, 8) NOT NULL,
                    volume      NUMERIC(28, 8) NOT NULL,
                    source      TEXT           NOT NULL
                );
                TRUNCATE _stage_price_ticks;
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Binary COPY into staging — bypasses SQL parsing and index maintenance.
        await using (var writer = await conn.BeginBinaryImportAsync(
            "COPY _stage_price_ticks (time, asset_id, symbol, exchange, bid, ask, last, volume, source) FROM STDIN (FORMAT BINARY)",
            cancellationToken))
        {
            foreach (var tick in unique.Values)
            {
                await writer.StartRowAsync(cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(tick.Time, NpgsqlDbType.TimestampTz, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(tick.AssetId, NpgsqlDbType.Uuid, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(tick.Symbol, NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(tick.Exchange, NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(tick.Bid, NpgsqlDbType.Numeric, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(tick.Ask, NpgsqlDbType.Numeric, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(tick.Last, NpgsqlDbType.Numeric, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(tick.Volume, NpgsqlDbType.Numeric, cancellationToken).ConfigureAwait(false);
                await writer.WriteAsync(tick.Source, NpgsqlDbType.Text, cancellationToken).ConfigureAwait(false);
            }
            await writer.CompleteAsync(cancellationToken).ConfigureAwait(false);
        }

        // Move from staging into the hypertable, skipping any duplicates (safe on consumer restart).
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO price_ticks (time, asset_id, symbol, exchange, bid, ask, last, volume, source)
                SELECT time, asset_id, symbol, exchange, bid, ask, last, volume, source
                FROM _stage_price_ticks
                ON CONFLICT (time, asset_id) DO NOTHING
                """;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildMarketKey(string symbol, string exchange) =>
        string.Concat(symbol.ToUpperInvariant(), "@", exchange.ToUpperInvariant());
}