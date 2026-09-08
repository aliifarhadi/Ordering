using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderAirTransportServiceDetailConfiguration : IEntityTypeConfiguration<OrderAirTransportServiceDetail>
    {
        public void Configure(EntityTypeBuilder<OrderAirTransportServiceDetail> builder)
        {
            builder.ToTable("OrderAirTransportServiceDetails");
            builder.HasKey(detail => detail.Id);
            builder.Property(detail => detail.Id).ValueGeneratedNever();
            builder.Property(detail => detail.TransitionalFareBasis).HasMaxLength(64);
            builder.Property(detail => detail.RequestedSeat).HasMaxLength(16);

            builder.HasIndex(detail => detail.OrderServiceId).IsUnique();
            builder.HasIndex(detail => detail.OrderSegmentId);

            builder.OwnsOne(detail => detail.TransitionalCheckedBaggage, baggage =>
            {
                baggage.Property(value => value.Weight).HasColumnName("TransitionalCheckedBaggageWeight");
                baggage.Property(value => value.Unit).HasColumnName("TransitionalCheckedBaggageUnit");
                baggage.Property(value => value.Pieces).HasColumnName("TransitionalCheckedBaggagePieces");
            });

            builder.OwnsOne(detail => detail.TransitionalCabinBaggage, baggage =>
            {
                baggage.Property(value => value.Weight).HasColumnName("TransitionalCabinBaggageWeight");
                baggage.Property(value => value.Unit).HasColumnName("TransitionalCabinBaggageUnit");
                baggage.Property(value => value.Pieces).HasColumnName("TransitionalCabinBaggagePieces");
            });
        }
    }
}
