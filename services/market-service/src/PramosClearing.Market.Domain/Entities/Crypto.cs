namespace PramosClearing.MarketService.Domain.Entities;

public sealed class Crypto : Asset
{
    public string Symbol { get; private set; }
    public string Network { get; private set; }
    public decimal? MaxSupply { get; private set; }

    public override AssetType AssetType => AssetType.Crypto;

    public Crypto(
        Guid id,
        string name,
        string currency,
        string symbol,
        string network,
        decimal? maxSupply = null)
        : base(id, name, currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol, nameof(symbol));
        ArgumentException.ThrowIfNullOrWhiteSpace(network, nameof(network));

        if (maxSupply.HasValue && maxSupply.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSupply),
                "Max supply must be greater than zero when specified.");

        Symbol = symbol.ToUpperInvariant();
        Network = network.ToUpperInvariant();
        MaxSupply = maxSupply;
    }

    public override string ToString() => Symbol;
}
