using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PramosClearing.UserService.Infrastructure.Persistence;

internal sealed class UserDbContextFactory : IDesignTimeDbContextFactory<UserDbContext>
{
    public UserDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<UserDbContext>()
            .UseSqlServer("Server=localhost;Database=PramosClearing_Users;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        return new UserDbContext(options);
    }
}
