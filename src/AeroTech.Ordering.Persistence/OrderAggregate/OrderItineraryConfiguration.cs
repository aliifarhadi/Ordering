using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderItineraryConfiguration : IEntityTypeConfiguration<OrderItinerary>
    {
        public void Configure(EntityTypeBuilder<OrderItinerary> builder)
        {
            builder.ToTable("OrderItineraries");
            builder.HasKey(itinerary => itinerary.Id);
            builder.Property(itinerary => itinerary.Id).ValueGeneratedNever();
            builder.Property(itinerary => itinerary.BoundId).HasMaxLength(256);
        }
    }
}
