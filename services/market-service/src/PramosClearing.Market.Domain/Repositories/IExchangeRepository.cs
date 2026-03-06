using PramosClearing.MarketService.Domain.Entities;

namespace PramosClearing.MarketService.Domain.Repositories;

public interface IExchangeRepository
{
    Task<Exchange?> GetByCodeAsync(string code, CancellationToken ct);

    Task AddAsync(Exchange exchange, CancellationToken ct);

    Task<bool> ExistsAsync(string code, CancellationToken ct);

    Task<IReadOnlyList<Exchange>> GetAllActiveAsync(CancellationToken ct);
}
