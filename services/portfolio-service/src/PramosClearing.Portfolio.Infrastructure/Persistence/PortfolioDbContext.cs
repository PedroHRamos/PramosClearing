using Microsoft.EntityFrameworkCore;
using PramosClearing.PortfolioService.Domain.Entities;

namespace PramosClearing.PortfolioService.Infrastructure.Persistence;

public sealed class PortfolioDbContext : DbContext
{
    public PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : base(options) { }

    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Position> Positions => Set<Position>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PortfolioDbContext).Assembly);
    }
}
