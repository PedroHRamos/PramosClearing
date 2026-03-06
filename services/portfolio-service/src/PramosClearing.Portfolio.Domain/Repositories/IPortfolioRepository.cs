using PramosClearing.PortfolioService.Domain.Entities;

namespace PramosClearing.PortfolioService.Domain.Repositories;

public interface IPortfolioRepository
{
    Task<Portfolio?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<Portfolio?> GetByUserIdAsync(Guid userId, CancellationToken ct);

    Task<bool> ExistsByUserIdAsync(Guid userId, CancellationToken ct);

    Task AddAsync(Portfolio portfolio, CancellationToken ct);
}
