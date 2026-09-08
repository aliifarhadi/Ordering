using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderServiceCoveredSegmentConfiguration : IEntityTypeConfiguration<OrderServiceCoveredSegment>
    {
        public void Configure(EntityTypeBuilder<OrderServiceCoveredSegment> builder)
        {
            builder.ToTable("OrderServiceCoveredSegments");
            builder.HasKey(covered => covered.Id);
            builder.Property(covered => covered.Id).ValueGeneratedNever();

            builder.HasIndex(covered => new { covered.OrderServiceId, covered.OrderSegmentId }).IsUnique();
        }
    }
}
