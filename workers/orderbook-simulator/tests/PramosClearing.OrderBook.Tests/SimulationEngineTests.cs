using Xunit;
using PramosClearing.OrderBook.Application.Models;
using PramosClearing.OrderBook.Application.Services;
using PramosClearing.OrderBook.Domain.Events;
using PramosClearing.OrderBook.Application.Ports;
using Microsoft.Extensions.Logging.Abstractions;

namespace PramosClearing.OrderBook.Tests;

public sealed class SimulationEngineTests
{
    private sealed class StubPublisher : IOrderBookEventPublisher
    {
        public List<OrderBookUpdate> Published { get; } = new();

        public Task PublishAsync(OrderBookUpdate update, CancellationToken ct)
        {
            Published.Add(update);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task TickAsync_DoesNothing_WhenNotInitialized()
    {
        var publisher = new StubPublisher();
        var engine    = new SimulationEngine(publisher, NullLogger<SimulationEngine>.Instance);

        await engine.TickAsync(CancellationToken.None);

        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task TickAsync_PublishesUpdate_AfterInitialization()
    {
        var publisher = new StubPublisher();
        var engine    = new SimulationEngine(publisher, NullLogger<SimulationEngine>.Instance);

        var stocks = new List<StockInfo>
        {
            new("AAPL", "NASDAQ", "USD", "Apple Inc."),
            new("MSFT", "NASDAQ", "USD", "Microsoft Corporation")
        };

        engine.Initialize(stocks);

        await engine.TickAsync(CancellationToken.None);

        Assert.Single(publisher.Published);
    }

    [Fact]
    public async Task TickAsync_PublishesUpdatesForKnownSymbols()
    {
        var publisher = new StubPublisher();
        var engine    = new SimulationEngine(publisher, NullLogger<SimulationEngine>.Instance);

        var stocks = new List<StockInfo>
        {
            new("GOOG", "NASDAQ", "USD", "Alphabet Inc.")
        };

        engine.Initialize(stocks);

        for (var i = 0; i < 50; i++)
            await engine.TickAsync(CancellationToken.None);

        Assert.All(publisher.Published, u => Assert.Equal("GOOG", u.Symbol));
        Assert.Equal(50, publisher.Published.Count);
    }
}
