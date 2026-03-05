using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PramosClearing.MarketService.Domain.Entities;

namespace PramosClearing.MarketService.Infrastructure.Persistence.Configurations;

internal sealed class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.ToTable("stocks");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .IsFixedLength();

        builder.Property(s => s.Symbol)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.Exchange)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.Sector)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(s => new { s.Symbol, s.Exchange })
            .IsUnique()
            .HasDatabaseName("ix_stocks_symbol_exchange");

        builder.HasIndex(s => s.Exchange)
            .HasDatabaseName("ix_stocks_exchange");
    }
}
