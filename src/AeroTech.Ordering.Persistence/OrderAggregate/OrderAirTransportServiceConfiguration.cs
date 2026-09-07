using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderAirTransportServiceConfiguration : IEntityTypeConfiguration<OrderAirTransportService>
    {
        public void Configure(EntityTypeBuilder<OrderAirTransportService> builder)
        {
            builder.ToTable("OrderAirTransportServices");
            builder.Property(service => service.Seat).HasMaxLength(16);
            builder.Property(service => service.FareBasis).HasMaxLength(64);
            builder.Property(service => service.FareFamilyTitle).HasMaxLength(128);

            builder.OwnsOne(service => service.Baggage);
            builder.OwnsOne(service => service.CabinBaggage);
        }
    }
}
