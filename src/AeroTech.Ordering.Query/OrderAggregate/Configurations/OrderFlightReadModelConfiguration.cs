using AeroTech.Ordering.Query.OrderAggregate.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Query.OrderAggregate.Configurations
{
    public sealed class OrderFlightReadModelConfiguration : IEntityTypeConfiguration<OrderFlightReadModel>
    {
        public void Configure(EntityTypeBuilder<OrderFlightReadModel> builder)
        {
            builder.ToTable("OrderFlights");
            builder.HasKey(flight => flight.Id);
            builder.Property(flight => flight.Id).ValueGeneratedNever();
            builder.Property(flight => flight.FlightNumber).HasMaxLength(20);
            builder.HasIndex(flight => flight.OrderId);
            builder.HasIndex(flight => flight.FlightNumber);
            builder.HasIndex(flight => flight.DepartureDateTime);
        }
    }
}
