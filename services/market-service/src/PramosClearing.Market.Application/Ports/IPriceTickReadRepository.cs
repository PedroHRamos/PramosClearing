using PramosClearing.MarketService.Application.Queries.Responses;

namespace PramosClearing.MarketService.Application.Ports;

public interface IPriceTickReadRepository
{
    Task<IReadOnlyList<PriceTickResponse>> GetLatestAsync(string symbol, string exchange, int take, CancellationToken ct);
    Task<PriceTickResponse?> GetLatestSingleAsync(string symbol, string exchange, CancellationToken ct);
}