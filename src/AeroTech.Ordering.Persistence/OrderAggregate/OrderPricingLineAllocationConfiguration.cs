using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderPricingLineAllocationConfiguration : IEntityTypeConfiguration<OrderPricingLineAllocation>
    {
        public void Configure(EntityTypeBuilder<OrderPricingLineAllocation> builder)
        {
            builder.ToTable("OrderPricingLineAllocations");
            builder.HasKey(allocation => allocation.Id);
            builder.Property(allocation => allocation.Id).ValueGeneratedNever();

            builder.OwnsOne(allocation => allocation.ExchangeRate, rate =>
            {
                rate.Property(value => value.RateOfExchange).HasColumnName("RateOfExchange");
                rate.Property(value => value.NumberOfDecimalPlaces).HasColumnName("NumberOfDecimalPlaces");
                rate.Property(value => value.RateOfExchangeId).HasColumnName("RateOfExchangeId");
                rate.Property(value => value.RoundingFactor).HasColumnName("RoundingFactor");
            });
        }
    }
}
