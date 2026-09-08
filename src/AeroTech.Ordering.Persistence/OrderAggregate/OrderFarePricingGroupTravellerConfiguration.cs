using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderFarePricingGroupTravellerConfiguration : IEntityTypeConfiguration<OrderFarePricingGroupTraveller>
    {
        public void Configure(EntityTypeBuilder<OrderFarePricingGroupTraveller> builder)
        {
            builder.ToTable("OrderFarePricingGroupTravellers");
            builder.HasKey(traveller => traveller.Id);
            builder.Property(traveller => traveller.Id).ValueGeneratedNever();

            builder.HasIndex(traveller => new { traveller.PricingGroupId, traveller.OrderTravellerId }).IsUnique();
            builder.HasIndex(traveller => traveller.OrderTravellerId);
        }
    }
}
