using PramosClearing.MarketService.Domain.Entities;

namespace PramosClearing.MarketService.Domain.Repositories;

public interface IStockRepository
{
    Task<Stock?> GetBySymbolAsync(string symbol, CancellationToken ct);

    Task AddAsync(Stock stock, CancellationToken ct);

    Task<bool> ExistsAsync(string symbol, CancellationToken ct);

    Task<IReadOnlyList<Stock>> GetByExchangeAsync(string exchange, CancellationToken ct);

    Task<(IReadOnlyList<Stock> Items, int TotalCount)> GetPagedBySymbolAsync(
        string symbolFilter, int page, int pageSize, CancellationToken ct);

    Task<Stock?> GetByIdAsync(Guid id, CancellationToken ct);

    Task UpdateAsync(Stock stock, CancellationToken ct);

    Task DeleteAsync(Stock stock, CancellationToken ct);
}
