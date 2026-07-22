using PramosClearing.MarketService.Application.Models;
using PramosClearing.MarketService.Application.Services;

namespace PramosClearing.Market.Tests;

public sealed class UnitTest1
{
    [Fact]
    public void TryProject_ReturnsFalse_UntilBothSidesExist()
    {
        var projector = new TopOfBookProjector();

        var published = projector.TryProject(
            new OrderBookUpdateMessage("AAPL", "NASDAQ", "bid", 182.30m, 100, "add", DateTime.UtcNow),
            Guid.NewGuid(),
            out var tick);

        Assert.False(published);
        Assert.Null(tick);
    }

    [Fact]
    public void TryProject_ReturnsBestBidAndAsk_AfterSnapshotLevels()
    {
        var projector = new TopOfBookProjector();
        var assetId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        projector.TryProject(
            new OrderBookUpdateMessage("AAPL", "NASDAQ", "bid", 182.30m, 100, "add", timestamp),
            assetId,
            out _);

        var published = projector.TryProject(
            new OrderBookUpdateMessage("AAPL", "NASDAQ", "ask", 182.40m, 120, "add", timestamp),
            assetId,
            out var tick);

        Assert.True(published);
        Assert.NotNull(tick);
        Assert.Equal(182.30m, tick!.Bid);
        Assert.Equal(182.40m, tick.Ask);
        Assert.Equal(182.35m, tick.Last);
    }

    [Fact]
    public void TryProject_UpdatesTopOfBook_WhenBestLevelIsRemoved()
    {
        var projector = new TopOfBookProjector();
        var assetId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        projector.TryProject(
            new OrderBookUpdateMessage("AAPL", "NASDAQ", "bid", 182.30m, 100, "add", timestamp),
            assetId,
            out _);
        projector.TryProject(
            new OrderBookUpdateMessage("AAPL", "NASDAQ", "bid", 182.20m, 150, "add", timestamp),
            assetId,
            out _);
        projector.TryProject(
            new OrderBookUpdateMessage("AAPL", "NASDAQ", "ask", 182.40m, 120, "add", timestamp),
            assetId,
            out _);

        var published = projector.TryProject(
            new OrderBookUpdateMessage("AAPL", "NASDAQ", "bid", 182.30m, 0, "remove", timestamp),
            assetId,
            out var tick);

        Assert.True(published);
        Assert.NotNull(tick);
        Assert.Equal(182.20m, tick!.Bid);
        Assert.Equal(182.40m, tick.Ask);
    }
}