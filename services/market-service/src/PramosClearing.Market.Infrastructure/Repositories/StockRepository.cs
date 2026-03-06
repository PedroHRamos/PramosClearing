using Microsoft.EntityFrameworkCore;
using PramosClearing.MarketService.Domain.Entities;
using PramosClearing.MarketService.Domain.Repositories;
using PramosClearing.MarketService.Infrastructure.Persistence;

namespace PramosClearing.MarketService.Infrastructure.Repositories;

public sealed class StockRepository : IStockRepository
{
    private readonly MarketDbContext _context;

    public StockRepository(MarketDbContext context)
    {
        _context = context;
    }

    public async Task<Stock?> GetBySymbolAsync(string symbol, CancellationToken ct) =>
        await _context.Stocks
            .FirstOrDefaultAsync(s => s.Symbol == symbol, ct)
            .ConfigureAwait(false);

    public async Task AddAsync(Stock stock, CancellationToken ct)
    {
        await _context.Stocks.AddAsync(stock, ct).ConfigureAwait(false);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string symbol, CancellationToken ct) =>
        await _context.Stocks
            .AnyAsync(s => s.Symbol == symbol, ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Stock>> GetByExchangeAsync(string exchange, CancellationToken ct) =>
        await _context.Stocks
            .Where(s => s.Exchange == exchange)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<(IReadOnlyList<Stock> Items, int TotalCount)> GetPagedBySymbolAsync(
        string symbolFilter, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.Stocks
            .Where(s => EF.Functions.Like(s.Symbol, $"%{symbolFilter.ToUpperInvariant()}%"));

        var totalCount = await query.CountAsync(ct).ConfigureAwait(false);

        var items = await query
            .OrderBy(s => s.Symbol)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (items, totalCount);
    }

    public async Task<Stock?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await _context.Stocks
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            .ConfigureAwait(false);

    public async Task UpdateAsync(Stock stock, CancellationToken ct)
    {
        _context.Stocks.Update(stock);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Stock stock, CancellationToken ct)
    {
        _context.Stocks.Update(stock);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
