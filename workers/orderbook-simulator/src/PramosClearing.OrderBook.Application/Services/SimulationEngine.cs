using Microsoft.Extensions.Logging;
using PramosClearing.OrderBook.Application.Models;
using PramosClearing.OrderBook.Application.Ports;
using PramosClearing.OrderBook.Domain.Events;
using OrderBookEntity = PramosClearing.OrderBook.Domain.Entities.OrderBook;

namespace PramosClearing.OrderBook.Application.Services;

public sealed class SimulationEngine
{
    private const int ConcurrencyLevel = 50;

    private readonly IOrderBookEventPublisher _publisher;
    private readonly ILogger<SimulationEngine> _logger;

    private readonly Dictionary<string, OrderBookEntity> _orderBooks = new();
    private readonly Dictionary<string, object> _locks = new();
    private string[] _symbols = Array.Empty<string>();

    public SimulationEngine(
        IOrderBookEventPublisher publisher,
        ILogger<SimulationEngine> logger)
    {
        _publisher = publisher;
        _logger    = logger;
    }

    public void Initialize(IReadOnlyList<StockInfo> stocks)
    {
        foreach (var stock in stocks)
        {
            var mid = GenerateInitialMidPrice();
            _orderBooks[stock.Symbol] = new OrderBookEntity(stock.Symbol, stock.Exchange, mid);
            _locks[stock.Symbol] = new object();
        }

        _symbols = _orderBooks.Keys.ToArray();
        _logger.LogInformation("Simulation initialized with {Count} order books.", _orderBooks.Count);
    }

    public async Task TickAsync(CancellationToken ct)
    {
        if (_symbols.Length == 0)
            return;

        var tasks = new Task[ConcurrencyLevel];
        for (var i = 0; i < ConcurrencyLevel; i++)
        {
            tasks[i] = GenerateAndPublishAsync(ct);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task GenerateAndPublishAsync(CancellationToken ct)
    {
        var symbol    = _symbols[Random.Shared.Next(_symbols.Length)];
        var orderBook = _orderBooks[symbol];

        OrderBookUpdate update;
        lock (_locks[symbol])
        {
            update = orderBook.GenerateUpdate(Random.Shared);
        }

        await _publisher.PublishAsync(update, ct).ConfigureAwait(false);
    }

    private static decimal GenerateInitialMidPrice() =>
        Math.Round(10m + (decimal)Random.Shared.NextDouble() * 990m, 2);
}
