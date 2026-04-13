using Microsoft.EntityFrameworkCore;
using PramosClearing.MarketService.Infrastructure.Persistence.Configurations;

namespace PramosClearing.MarketService.Infrastructure.Persistence;

public sealed class MarketDataDbContext : DbContext
{
    public MarketDataDbContext(DbContextOptions<MarketDataDbContext> options)
        : base(options)
    {
    }

    internal DbSet<PriceTickEntity> PriceTicks => Set<PriceTickEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PriceTickConfiguration());
    }
}