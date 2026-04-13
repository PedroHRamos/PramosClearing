using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PramosClearing.MarketService.Infrastructure.Persistence.Configurations;

internal sealed class PriceTickConfiguration : IEntityTypeConfiguration<PriceTickEntity>
{
    public void Configure(EntityTypeBuilder<PriceTickEntity> builder)
    {
        builder.ToTable("price_ticks");

        builder.HasKey(tick => new
        {
            tick.Time,
            tick.AssetId,
            tick.Symbol,
            tick.Exchange,
            tick.Bid,
            tick.Ask,
            tick.Last,
            tick.Volume,
            tick.Source
        });

        builder.Property(tick => tick.Time)
            .HasColumnName("time");

        builder.Property(tick => tick.AssetId)
            .HasColumnName("asset_id");

        builder.Property(tick => tick.Symbol)
            .HasColumnName("symbol")
            .HasMaxLength(20);

        builder.Property(tick => tick.Exchange)
            .HasColumnName("exchange")
            .HasMaxLength(20);

        builder.Property(tick => tick.Bid)
            .HasColumnName("bid")
            .HasColumnType("numeric(28, 8)");

        builder.Property(tick => tick.Ask)
            .HasColumnName("ask")
            .HasColumnType("numeric(28, 8)");

        builder.Property(tick => tick.Last)
            .HasColumnName("last")
            .HasColumnType("numeric(28, 8)");

        builder.Property(tick => tick.Volume)
            .HasColumnName("volume")
            .HasColumnType("numeric(28, 8)");

        builder.Property(tick => tick.Source)
            .HasColumnName("source")
            .HasMaxLength(50);
    }
}