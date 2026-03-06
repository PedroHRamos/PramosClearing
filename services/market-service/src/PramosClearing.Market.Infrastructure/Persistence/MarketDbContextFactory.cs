using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PramosClearing.MarketService.Infrastructure.Persistence;

internal sealed class MarketDbContextFactory : IDesignTimeDbContextFactory<MarketDbContext>
{
    public MarketDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MarketDbContext>()
            .UseSqlServer("Server=localhost;Database=PramosClearing_Market;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new MarketDbContext(options);
    }
}
