using Microsoft.EntityFrameworkCore;

namespace PramosClearing.OrderBook.Infrastructure.Persistence;

public sealed class MarketReadDbContext : DbContext
{
    public MarketReadDbContext(DbContextOptions<MarketReadDbContext> options) : base(options) { }

    internal DbSet<StockProjection> StockProjections => Set<StockProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StockProjection>()
            .HasNoKey()
            .ToSqlQuery("""
                SELECT
                    s.symbol   AS Symbol,
                    s.exchange AS Exchange,
                    a.currency AS Currency,
                    a.name     AS Name
                FROM assets a
                INNER JOIN stocks s ON a.id = s.id
                WHERE a.IsActive = 1
                """);
    }
}
