using PramosClearing.MarketService.Domain.Entities;

namespace PramosClearing.MarketService.Domain.Repositories;

public interface ICryptoRepository
{
    Task<Crypto?> GetBySymbolAsync(string symbol, CancellationToken ct);

    Task AddAsync(Crypto crypto, CancellationToken ct);

    Task<bool> ExistsAsync(string symbol, CancellationToken ct);

    Task<IReadOnlyList<Crypto>> GetByNetworkAsync(string network, CancellationToken ct);
}
