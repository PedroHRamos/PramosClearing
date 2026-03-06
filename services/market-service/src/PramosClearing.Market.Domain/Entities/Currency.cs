namespace PramosClearing.MarketService.Domain.Entities;

public sealed class Currency
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string Symbol { get; private set; }
    public bool IsActive { get; private set; }

    public Currency(string code, string name, string symbol)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol, nameof(symbol));

        Code     = code.ToUpperInvariant();
        Name     = name;
        Symbol   = symbol;
        IsActive = true;
    }

    private Currency()
    {
        Code   = null!;
        Name   = null!;
        Symbol = null!;
    }
}
