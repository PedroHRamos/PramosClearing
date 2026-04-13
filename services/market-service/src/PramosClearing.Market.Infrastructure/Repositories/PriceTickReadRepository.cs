using Microsoft.EntityFrameworkCore;
using PramosClearing.MarketService.Application.Ports;
using PramosClearing.MarketService.Application.Queries.Responses;
using PramosClearing.MarketService.Infrastructure.Persistence;

namespace PramosClearing.MarketService.Infrastructure.Repositories;

public sealed class PriceTickReadRepository : IPriceTickReadRepository
{
    private readonly IDbContextFactory<MarketDataDbContext> _dbContextFactory;

    public PriceTickReadRepository(IDbContextFactory<MarketDataDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<IReadOnlyList<PriceTickResponse>> GetLatestAsync(string symbol, string exchange, int take, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        return await db.Set<PriceTickEntity>()
            .AsNoTracking()
            .Where(tick => tick.Symbol == symbol && tick.Exchange == exchange)
            .OrderByDescending(tick => tick.Time)
            .Take(take)
            .Select(tick => new PriceTickResponse(
                tick.Time,
                tick.AssetId,
                tick.Symbol,
                tick.Exchange,
                tick.Bid,
                tick.Ask,
                tick.Last,
                tick.Volume,
                tick.Source))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<PriceTickResponse?> GetLatestSingleAsync(string symbol, string exchange, CancellationToken ct)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        return await db.Set<PriceTickEntity>()
            .AsNoTracking()
            .Where(tick => tick.Symbol == symbol && tick.Exchange == exchange)
            .OrderByDescending(tick => tick.Time)
            .Select(tick => new PriceTickResponse(
                tick.Time,
                tick.AssetId,
                tick.Symbol,
                tick.Exchange,
                tick.Bid,
                tick.Ask,
                tick.Last,
                tick.Volume,
                tick.Source))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
}