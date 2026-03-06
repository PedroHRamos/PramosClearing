using Microsoft.EntityFrameworkCore;
using PramosClearing.MarketService.Domain.Entities;

namespace PramosClearing.MarketService.Infrastructure.Persistence;

public sealed class MarketDbContext : DbContext
{
    public MarketDbContext(DbContextOptions<MarketDbContext> options) : base(options) { }

    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Etf> Etfs => Set<Etf>();
    public DbSet<Crypto> Cryptos => Set<Crypto>();
    public DbSet<Exchange> Exchanges => Set<Exchange>();
    public DbSet<Currency> Currencies => Set<Currency>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MarketDbContext).Assembly);
    }
}
