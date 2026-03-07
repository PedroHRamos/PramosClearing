namespace PramosClearing.OrderBook.Infrastructure.Persistence;

internal sealed class StockProjection
{
    public string Symbol   { get; set; } = null!;
    public string Exchange { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string Name     { get; set; } = null!;
}
