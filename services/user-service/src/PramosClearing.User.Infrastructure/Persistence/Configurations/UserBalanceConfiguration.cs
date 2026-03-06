using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PramosClearing.UserService.Domain.Entities;

namespace PramosClearing.UserService.Infrastructure.Persistence.Configurations;

internal sealed class UserBalanceConfiguration : IEntityTypeConfiguration<UserBalance>
{
    public void Configure(EntityTypeBuilder<UserBalance> builder)
    {
        builder.ToTable("user_balances");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .ValueGeneratedNever();

        builder.Property(b => b.UserId)
            .IsRequired();

        builder.Property(b => b.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength();

        builder.Property(b => b.Amount)
            .IsRequired()
            .HasPrecision(28, 8);

        builder.Property(b => b.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(b => new { b.UserId, b.Currency })
            .IsUnique()
            .HasDatabaseName("ix_user_balances_user_currency");
    }
}
