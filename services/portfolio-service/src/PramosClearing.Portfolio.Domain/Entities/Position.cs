namespace PramosClearing.PortfolioService.Domain.Entities;

public sealed class Position
{
    public Guid Id { get; private set; }
    public Guid PortfolioId { get; private set; }
    public Guid AssetId { get; private set; }
    public string AssetSymbol { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal AverageCostPrice { get; private set; }
    public decimal TotalCost { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public Position(
        Guid id,
        Guid portfolioId,
        Guid assetId,
        string assetSymbol,
        decimal quantity,
        decimal averageCostPrice)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Position id must not be empty.", nameof(id));

        if (portfolioId == Guid.Empty)
            throw new ArgumentException("PortfolioId must not be empty.", nameof(portfolioId));

        if (assetId == Guid.Empty)
            throw new ArgumentException("AssetId must not be empty.", nameof(assetId));

        ArgumentException.ThrowIfNullOrWhiteSpace(assetSymbol, nameof(assetSymbol));

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

        if (averageCostPrice < 0)
            throw new ArgumentOutOfRangeException(nameof(averageCostPrice), "Average cost price must be zero or greater.");

        Id               = id;
        PortfolioId      = portfolioId;
        AssetId          = assetId;
        AssetSymbol      = assetSymbol.ToUpperInvariant();
        Quantity         = quantity;
        AverageCostPrice = averageCostPrice;
        TotalCost        = quantity * averageCostPrice;
        UpdatedAt        = DateTime.UtcNow;
    }

    private Position()
    {
        AssetSymbol = null!;
    }
}
