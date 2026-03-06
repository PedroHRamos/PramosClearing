using PramosClearing.PortfolioService.Domain.Entities;

namespace PramosClearing.PortfolioService.Domain.Repositories;

public interface IPositionRepository
{
    Task<Position?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Position>> GetByPortfolioIdAsync(Guid portfolioId, CancellationToken ct);

    Task<Position?> GetByPortfolioAndAssetAsync(Guid portfolioId, Guid assetId, CancellationToken ct);

    Task AddAsync(Position position, CancellationToken ct);
}
