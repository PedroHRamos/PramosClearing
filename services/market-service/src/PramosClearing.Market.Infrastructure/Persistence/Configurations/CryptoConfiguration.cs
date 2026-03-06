using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PramosClearing.MarketService.Domain.Entities;

namespace PramosClearing.MarketService.Infrastructure.Persistence.Configurations;

internal sealed class CryptoConfiguration : IEntityTypeConfiguration<Crypto>
{
    public void Configure(EntityTypeBuilder<Crypto> builder)
    {
        builder.ToTable("cryptos");

        builder.Property(c => c.Symbol)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(c => c.Network)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.MaxSupply)
            .IsRequired(false)
            .HasPrecision(28, 8);

        builder.HasIndex(c => c.Symbol)
            .IsUnique()
            .HasDatabaseName("ix_cryptos_symbol");

        builder.HasIndex(c => c.Network)
            .HasDatabaseName("ix_cryptos_network");
    }
}
