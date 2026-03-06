using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace PramosClearing.MarketService.Infrastructure.Persistence;

internal sealed class MarketDbContextFactory : IDesignTimeDbContextFactory<MarketDbContext>
{
    private readonly string _connectionString;

    public MarketDbContextFactory()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json.");
    }

    public MarketDbContextFactory(IOptions<ConnectionStringsOptions> options)
    {
        _connectionString = options.Value.DefaultConnection
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    public MarketDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MarketDbContext>()
            .UseSqlServer(_connectionString)
            .Options;

        return new MarketDbContext(options);
    }
}
