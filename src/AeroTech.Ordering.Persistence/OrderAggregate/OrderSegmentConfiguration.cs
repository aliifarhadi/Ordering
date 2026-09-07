using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderSegmentConfiguration : IEntityTypeConfiguration<OrderSegment>
    {
        public void Configure(EntityTypeBuilder<OrderSegment> builder)
        {
            builder.ToTable("OrderSegments");
            builder.HasKey(segment => segment.Id);
            builder.Property(segment => segment.Id).ValueGeneratedNever();
            builder.Property(segment => segment.Number).HasMaxLength(16);
            builder.Property(segment => segment.BookingClass).HasMaxLength(16);
            builder.Property(segment => segment.BookingClassCode).HasMaxLength(16);
            builder.Ignore(segment => segment.FlightVersionId);

            builder.HasMany(segment => segment.Legs).WithOne().HasForeignKey(leg => leg.OrderSegmentId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(segment => segment.Legs).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
