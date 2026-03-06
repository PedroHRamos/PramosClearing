using PramosClearing.MarketService.Domain.Entities;

namespace PramosClearing.MarketService.Domain.Repositories;

public interface IEtfRepository
{
    Task<Etf?> GetBySymbolAndExchangeAsync(string symbol, string exchange, CancellationToken ct);

    Task AddAsync(Etf etf, CancellationToken ct);

    Task<bool> ExistsAsync(string symbol, string exchange, CancellationToken ct);

    Task<IReadOnlyList<Etf>> GetByExchangeAsync(string exchange, CancellationToken ct);
}
