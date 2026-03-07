using Microsoft.EntityFrameworkCore;
using PramosClearing.OrderBook.Application.Models;
using PramosClearing.OrderBook.Application.Ports;
using PramosClearing.OrderBook.Infrastructure.Persistence;

namespace PramosClearing.OrderBook.Infrastructure;

public sealed class StockLoader : IStockLoader
{
    private readonly MarketReadDbContext _context;

    public StockLoader(MarketReadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<StockInfo>> LoadAllActiveAsync(CancellationToken ct)
    {
        var projections = await _context.StockProjections
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return projections
            .Select(p => new StockInfo(p.Symbol, p.Exchange, p.Currency, p.Name))
            .ToList();
    }
}
