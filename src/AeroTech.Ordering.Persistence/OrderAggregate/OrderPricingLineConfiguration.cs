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
            builder.Property(line => line.Reference).HasMaxLength(256);

            builder.OwnsOne(line => line.ExchangeRate, rate =>
            {
                rate.Property(value => value.RateOfExchange).HasColumnName("RateOfExchange");
                rate.Property(value => value.NumberOfDecimalPlaces).HasColumnName("NumberOfDecimalPlaces");
                rate.Property(value => value.RateOfExchangeId).HasColumnName("RateOfExchangeId");
                rate.Property(value => value.RoundingFactor).HasColumnName("RoundingFactor");
            });

            builder.HasMany(line => line.Allocations).WithOne().HasForeignKey(allocation => allocation.OrderPricingLineId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(line => line.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
