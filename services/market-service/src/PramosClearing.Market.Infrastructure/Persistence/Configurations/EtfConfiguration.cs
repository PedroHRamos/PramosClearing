using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PramosClearing.MarketService.Domain.Entities;

namespace PramosClearing.MarketService.Infrastructure.Persistence.Configurations;

internal sealed class EtfConfiguration : IEntityTypeConfiguration<Etf>
{
    public void Configure(EntityTypeBuilder<Etf> builder)
    {
        builder.ToTable("etfs");

        builder.Property(e => e.Symbol)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.Exchange)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.UnderlyingIndex)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.TotalExpenseRatio)
            .IsRequired()
            .HasPrecision(8, 6);

        builder.Property(e => e.MarketIdentifier)
            .IsRequired()
            .HasMaxLength(42);

        builder.HasIndex(e => new { e.Symbol, e.Exchange })
            .IsUnique()
            .HasDatabaseName("ix_etfs_symbol_exchange");

        builder.HasIndex(e => e.Exchange)
            .HasDatabaseName("ix_etfs_exchange");
    }
}
