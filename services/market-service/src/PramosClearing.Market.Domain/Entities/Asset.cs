namespace PramosClearing.MarketService.Domain.Entities;

public abstract class Asset
{
    public Guid Id { get; protected set; }
    public string Name { get; protected set; }
    public string Currency { get; protected set; }
    public bool IsActive { get; protected set; }
    public DateTime CreatedAt { get; protected set; }
    public DateTime? DeletedAt { get; protected set; }

    public abstract AssetType AssetType { get; }

    protected Asset(Guid id, string name, string currency)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Asset id must not be empty.", nameof(id));

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(currency, nameof(currency));

        Id        = id;
        Name      = name;
        Currency  = currency;
        IsActive  = true;
        CreatedAt = DateTime.UtcNow;
    }

    protected Asset()
    {
        Name     = null!;
        Currency = null!;
    }

    public void Deactivate()
    {
        IsActive  = false;
        DeletedAt = DateTime.UtcNow;
    }
}
