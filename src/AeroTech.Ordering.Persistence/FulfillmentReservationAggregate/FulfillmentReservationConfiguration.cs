using AeroTech.Ordering.Domain.FulfillmentReservationAggregate;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.FulfillmentReservationAggregate
{
    public sealed class FulfillmentReservationConfiguration : IEntityTypeConfiguration<FulfillmentReservation>
    {
        public void Configure(EntityTypeBuilder<FulfillmentReservation> builder)
        {
            builder.ToTable("FulfillmentReservations");
            builder.HasKey(reservation => reservation.Id);
            builder.Property(reservation => reservation.Id).ValueGeneratedNever();
            builder.Property(reservation => reservation.ProviderCode).HasMaxLength(32);
            builder.Property(reservation => reservation.ExternalReservationRef).HasMaxLength(128);

            builder.HasIndex(reservation => reservation.OrderId);
            builder.HasIndex(reservation => reservation.OperationId).IsUnique();

            builder.HasMany(reservation => reservation.Services)
                .WithOne()
                .HasForeignKey(service => service.FulfillmentReservationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(reservation => reservation.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public sealed class FulfillmentReservationServiceConfiguration : IEntityTypeConfiguration<FulfillmentReservationService>
    {
        public void Configure(EntityTypeBuilder<FulfillmentReservationService> builder)
        {
            builder.ToTable("FulfillmentReservationServices");
            builder.HasKey(service => service.Id);
            builder.Property(service => service.Id).ValueGeneratedNever();
            builder.Property(service => service.ExternalServiceRef).HasMaxLength(128);
            builder.Property(service => service.ObservedBookingClass).HasMaxLength(8);
            builder.Property(service => service.ExternalStatus).HasMaxLength(64);

            builder.HasIndex(service => new { service.FulfillmentReservationId, service.OrderServiceId }).IsUnique();
            builder.HasIndex(service => service.OrderServiceId);
        }
    }
}
