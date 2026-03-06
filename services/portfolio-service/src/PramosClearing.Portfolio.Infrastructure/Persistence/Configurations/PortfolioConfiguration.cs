using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PramosClearing.PortfolioService.Domain.Entities;

namespace PramosClearing.PortfolioService.Infrastructure.Persistence.Configurations;

internal sealed class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("portfolios");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.UserId)
            .IsRequired();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.BaseCurrency)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength();

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Property(p => p.UpdatedAt)
            .IsRequired(false);

        builder.HasIndex(p => p.UserId)
            .IsUnique()
            .HasDatabaseName("ix_portfolios_user_id");

        builder.HasMany(p => p.Positions)
            .WithOne()
            .HasForeignKey(pos => pos.PortfolioId)
            .HasConstraintName("fk_positions_portfolios")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
