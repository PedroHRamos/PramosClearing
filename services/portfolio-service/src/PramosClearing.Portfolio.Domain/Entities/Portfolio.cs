namespace PramosClearing.PortfolioService.Domain.Entities;

public sealed class Portfolio
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public string BaseCurrency { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private readonly List<Position> _positions = new();
    public IReadOnlyList<Position> Positions => _positions.AsReadOnly();

    public Portfolio(Guid id, Guid userId, string name, string baseCurrency)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Portfolio id must not be empty.", nameof(id));

        if (userId == Guid.Empty)
            throw new ArgumentException("UserId must not be empty.", nameof(userId));

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentException.ThrowIfNullOrWhiteSpace(baseCurrency, nameof(baseCurrency));

        Id           = id;
        UserId       = userId;
        Name         = name;
        BaseCurrency = baseCurrency.ToUpperInvariant();
        CreatedAt    = DateTime.UtcNow;
    }

    private Portfolio()
    {
        Name         = null!;
        BaseCurrency = null!;
    }
}
