using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderPricingAllocationConfiguration : IEntityTypeConfiguration<OrderPricingAllocation>
    {
        public void Configure(EntityTypeBuilder<OrderPricingAllocation> builder)
        {
            builder.ToTable("OrderPricingAllocations");
            builder.HasKey(allocation => allocation.Id);
            builder.Property(allocation => allocation.Id).ValueGeneratedNever();
            builder.Property(allocation => allocation.CoveragePortionRef).HasMaxLength(256);

            builder.HasIndex(allocation => allocation.OrderServiceId);

            builder.OwnsOne(allocation => allocation.ExchangeRate, rate =>
            {
                rate.Property(value => value.RateOfExchange).HasColumnName("RateOfExchange").HasPrecision(28, 12);
                rate.Property(value => value.NumberOfDecimalPlaces).HasColumnName("NumberOfDecimalPlaces");
                rate.Property(value => value.RateOfExchangeId).HasColumnName("RateOfExchangeId");
                rate.Property(value => value.RoundingFactor).HasColumnName("RoundingFactor");
            });
        }
    }
}
