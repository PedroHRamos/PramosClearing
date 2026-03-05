using PramosClearing.MarketService.Domain.Entities;

namespace PramosClearing.MarketService.Domain.Repositories;

public interface IStockRepository
{
    Task<Stock?> GetBySymbolAsync(string symbol, CancellationToken ct);

    Task AddAsync(Stock stock, CancellationToken ct);

    Task<bool> ExistsAsync(string symbol, CancellationToken ct);

    Task<IReadOnlyList<Stock>> GetByExchangeAsync(string exchange, CancellationToken ct);
}
