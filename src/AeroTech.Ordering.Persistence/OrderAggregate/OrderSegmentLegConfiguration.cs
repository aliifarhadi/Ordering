using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderSegmentLegConfiguration : IEntityTypeConfiguration<OrderSegmentLeg>
    {
        public void Configure(EntityTypeBuilder<OrderSegmentLeg> builder)
        {
            builder.ToTable("OrderSegmentLegs");
            builder.HasKey(leg => leg.Id);
            builder.Property(leg => leg.Id).ValueGeneratedNever();
        }
    }
}
