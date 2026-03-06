using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PramosClearing.PortfolioService.Domain.Entities;

namespace PramosClearing.PortfolioService.Infrastructure.Persistence.Configurations;

internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("positions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.PortfolioId)
            .IsRequired();

        builder.Property(p => p.AssetId)
            .IsRequired();

        builder.Property(p => p.AssetSymbol)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(p => p.Quantity)
            .IsRequired()
            .HasPrecision(28, 8);

        builder.Property(p => p.AverageCostPrice)
            .IsRequired()
            .HasPrecision(28, 8);

        builder.Property(p => p.TotalCost)
            .IsRequired()
            .HasPrecision(28, 8);

        builder.Property(p => p.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(p => new { p.PortfolioId, p.AssetId })
            .IsUnique()
            .HasDatabaseName("ix_positions_portfolio_asset");

        builder.HasIndex(p => p.AssetId)
            .HasDatabaseName("ix_positions_asset_id");
    }
}
