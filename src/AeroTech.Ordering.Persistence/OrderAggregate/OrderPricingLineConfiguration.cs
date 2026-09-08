using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderPricingLineConfiguration : IEntityTypeConfiguration<OrderPricingLine>
    {
        public void Configure(EntityTypeBuilder<OrderPricingLine> builder)
        {
            builder.ToTable("OrderPricingLines");
            builder.HasKey(line => line.Id);
            builder.Property(line => line.Id).ValueGeneratedNever();
            builder.Property(line => line.Code).HasMaxLength(256);
            builder.Property(line => line.Description).HasMaxLength(256);
            builder.Property(line => line.UnitOfMeasure).HasMaxLength(32);
            builder.Property(line => line.SourceLineRef).HasMaxLength(512);
            builder.Property(line => line.OccurrenceKey).HasMaxLength(64);
            builder.Property(line => line.TransferGroupId).HasMaxLength(64);
            builder.Property(line => line.SettlementPartyRef).HasMaxLength(256);
            builder.Property(line => line.SettlementCategory).HasMaxLength(128);
            builder.Property(line => line.UnitPrice).HasPrecision(28, 12);

            builder.HasIndex(line => line.PriceChangeSetId);
            builder.HasIndex(line => line.OriginalPricingLineId);
            builder.HasIndex(line => new { line.PriceChangeSetId, line.SourceLineRef, line.OccurrenceKey })
                .IsUnique()
                .HasFilter("[SourceLineRef] IS NOT NULL");

            builder.OwnsOne(line => line.ExchangeRate, rate =>
            {
                rate.Property(value => value.RateOfExchange).HasColumnName("RateOfExchange").HasPrecision(28, 12);
                rate.Property(value => value.NumberOfDecimalPlaces).HasColumnName("NumberOfDecimalPlaces");
                rate.Property(value => value.RateOfExchangeId).HasColumnName("RateOfExchangeId");
                rate.Property(value => value.RoundingFactor).HasColumnName("RoundingFactor");
            });

            builder.HasMany(line => line.AllocationSets)
                .WithOne()
                .HasForeignKey(set => set.OrderPricingLineId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(line => line.AllocationSets).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
