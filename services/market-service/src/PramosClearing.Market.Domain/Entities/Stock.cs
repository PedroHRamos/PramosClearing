namespace PramosClearing.MarketService.Domain.Entities;

public sealed class Stock : Asset
{
    public string Symbol { get; private set; }
    public string Exchange { get; private set; }
    public string Sector { get; private set; }

    public string MarketIdentifier { get; private set; }

    public Stock(
        Guid id,
        string name,
        string currency,
        string symbol,
        string exchange,
        string sector)
        : base(id, name, currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol, nameof(symbol));
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange, nameof(exchange));
        ArgumentException.ThrowIfNullOrWhiteSpace(sector, nameof(sector));

        Symbol   = symbol.ToUpperInvariant();
        Exchange = exchange.ToUpperInvariant();
        Sector   = sector;
        MarketIdentifier = string.Concat(Symbol, "@", Exchange);
    }

    public override AssetType AssetType => AssetType.Stock;

    public override string ToString() => MarketIdentifier;

    private Stock()
    {
        Symbol           = null!;
        Exchange         = null!;
        Sector           = null!;
        MarketIdentifier = null!;
    }
}
