using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderFareComponentSegmentConfiguration : IEntityTypeConfiguration<OrderFareComponentSegment>
    {
        public void Configure(EntityTypeBuilder<OrderFareComponentSegment> builder)
        {
            builder.ToTable("OrderFareComponentSegments");
            builder.HasKey(segment => segment.Id);
            builder.Property(segment => segment.Id).ValueGeneratedNever();

            builder.HasIndex(segment => new { segment.FareComponentId, segment.OrderSegmentId }).IsUnique();
        }
    }
}
