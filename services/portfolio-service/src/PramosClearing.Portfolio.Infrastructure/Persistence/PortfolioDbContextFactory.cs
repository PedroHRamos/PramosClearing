using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PramosClearing.PortfolioService.Infrastructure.Persistence;

internal sealed class PortfolioDbContextFactory : IDesignTimeDbContextFactory<PortfolioDbContext>
{
    public PortfolioDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseSqlServer("Server=localhost;Database=PramosClearing_Portfolio;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new PortfolioDbContext(options);
    }
}
