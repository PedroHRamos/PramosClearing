using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PramosClearing.MarketService.Infrastructure.Persistence;

internal sealed class MarketDbContextFactory : IDesignTimeDbContextFactory<MarketDbContext>
{
    public MarketDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json.");

        var options = new DbContextOptionsBuilder<MarketDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new MarketDbContext(options);
    }
}
