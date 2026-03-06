namespace PramosClearing.MarketService.Domain.Entities;

public sealed class Exchange
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string Country { get; private set; }
    public string Timezone { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Exchange(string code, string name, string country, string timezone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code, nameof(code));
        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(country, nameof(country));
        ArgumentException.ThrowIfNullOrWhiteSpace(timezone, nameof(timezone));

        Code      = code.ToUpperInvariant();
        Name      = name;
        Country   = country.ToUpperInvariant();
        Timezone  = timezone;
        IsActive  = true;
        CreatedAt = DateTime.UtcNow;
    }

    private Exchange()
    {
        Code     = null!;
        Name     = null!;
        Country  = null!;
        Timezone = null!;
    }
}
