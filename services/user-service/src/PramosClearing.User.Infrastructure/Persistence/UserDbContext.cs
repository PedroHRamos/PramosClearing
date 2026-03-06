using Microsoft.EntityFrameworkCore;
using PramosClearing.UserService.Domain.Entities;

namespace PramosClearing.UserService.Infrastructure.Persistence;

public sealed class UserDbContext : DbContext
{
    public UserDbContext(DbContextOptions<UserDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserBalance> UserBalances => Set<UserBalance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UserDbContext).Assembly);
    }
}
