namespace PramosClearing.MarketService.Domain.Entities;

public sealed class Etf : Asset
{
    public string Symbol { get; private set; }
    public string Exchange { get; private set; }
    public string UnderlyingIndex { get; private set; }
    public decimal TotalExpenseRatio { get; private set; }

    public override AssetType AssetType => AssetType.Etf;

    public Etf(
        Guid id,
        string name,
        string currency,
        string symbol,
        string exchange,
        string underlyingIndex,
        decimal totalExpenseRatio)
        : base(id, name, currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol, nameof(symbol));
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange, nameof(exchange));
        ArgumentException.ThrowIfNullOrWhiteSpace(underlyingIndex, nameof(underlyingIndex));

        if (totalExpenseRatio < 0)
            throw new ArgumentOutOfRangeException(nameof(totalExpenseRatio),
                "Total expense ratio must be zero or greater.");

        Symbol = symbol.ToUpperInvariant();
        Exchange = exchange.ToUpperInvariant();
        UnderlyingIndex = underlyingIndex;
        TotalExpenseRatio = totalExpenseRatio;
        MarketIdentifier = string.Concat(Symbol, "@", Exchange);
    }

    public string MarketIdentifier { get; private set; }

    public override string ToString() => MarketIdentifier;

    private Etf()
    {
        Symbol           = null!;
        Exchange         = null!;
        UnderlyingIndex  = null!;
        MarketIdentifier = null!;
    }
}
