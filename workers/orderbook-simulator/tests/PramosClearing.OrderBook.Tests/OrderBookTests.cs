using Xunit;
using PramosClearing.OrderBook.Domain.Enums;
using OrderBookEntity = PramosClearing.OrderBook.Domain.Entities.OrderBook;

namespace PramosClearing.OrderBook.Tests;

public sealed class OrderBookTests
{
    [Fact]
    public void Constructor_InitializesWithSeedLevels()
    {
        var book = new OrderBookEntity("AAPL", "NASDAQ", 182.50m);

        Assert.NotEmpty(book.Bids);
        Assert.NotEmpty(book.Asks);
    }

    [Fact]
    public void Constructor_ThrowsOnEmptySymbol()
    {
        Assert.Throws<ArgumentException>(() => new OrderBookEntity("", "NASDAQ", 100m));
    }

    [Fact]
    public void Constructor_ThrowsOnNonPositiveMidPrice()
    {
        Assert.Throws<ArgumentException>(() => new OrderBookEntity("AAPL", "NASDAQ", 0m));
    }

    [Fact]
    public void GenerateUpdate_ReturnsBidOrAskUpdate()
    {
        var book   = new OrderBookEntity("AAPL", "NASDAQ", 182.50m);
        var random = new Random(42);

        var update = book.GenerateUpdate(random);

        Assert.Equal("AAPL", update.Symbol);
        Assert.Equal("NASDAQ", update.Exchange);
        Assert.True(update.Side == OrderSide.Bid || update.Side == OrderSide.Ask);
        Assert.True(Enum.IsDefined(update.Action));
        Assert.True(update.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void GenerateUpdate_RemoveActionHasZeroSize()
    {
        var book   = new OrderBookEntity("TSLA", "NASDAQ", 250m);
        var random = new Random(1);

        for (var i = 0; i < 500; i++)
        {
            var update = book.GenerateUpdate(random);
            if (update.Action == OrderAction.Remove)
            {
                Assert.Equal(0, update.Size);
                return;
            }
        }
    }

    [Fact]
    public void GenerateUpdate_AddUpdateActionHasPositiveSize()
    {
        var book   = new OrderBookEntity("MSFT", "NASDAQ", 400m);
        var random = new Random(99);

        for (var i = 0; i < 200; i++)
        {
            var update = book.GenerateUpdate(random);
            if (update.Action != OrderAction.Remove)
                Assert.True(update.Size > 0);
        }
    }

    [Fact]
    public void GenerateUpdate_BidPricesAreBelowAskPrices()
    {
        var book   = new OrderBookEntity("GOOG", "NASDAQ", 150m);
        var random = new Random(7);

        for (var i = 0; i < 1000; i++)
            book.GenerateUpdate(random);

        if (book.Bids.Count > 0 && book.Asks.Count > 0)
        {
            var bestBid = book.Bids.Keys.First();
            var bestAsk = book.Asks.Keys.First();
            Assert.True(bestBid < bestAsk, $"Best bid {bestBid} must be below best ask {bestAsk}");
        }
    }

    [Theory]
    [InlineData("AAPL", "NASDAQ", 182.50)]
    [InlineData("BTC",  "CRYPTO", 60000.00)]
    [InlineData("SPY",  "NYSE",   500.00)]
    public void Constructor_StoresSymbolAndExchange(string symbol, string exchange, double mid)
    {
        var book = new OrderBookEntity(symbol, exchange, (decimal)mid);

        Assert.Equal(symbol,   book.Symbol);
        Assert.Equal(exchange, book.Exchange);
    }
}
