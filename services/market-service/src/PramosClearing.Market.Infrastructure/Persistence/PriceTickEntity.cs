namespace PramosClearing.MarketService.Infrastructure.Persistence;

internal sealed class PriceTickEntity
{
    public DateTime Time { get; set; }
    public Guid AssetId { get; set; }
    public string Symbol { get; set; } = null!;
    public string Exchange { get; set; } = null!;
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal Last { get; set; }
    public decimal Volume { get; set; }
    public string Source { get; set; } = null!;
}