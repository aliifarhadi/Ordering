using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.ElectronicTicketAggregate
{
    public sealed class ElectronicTicketConfiguration : IEntityTypeConfiguration<ElectronicTicket>
    {
        public void Configure(EntityTypeBuilder<ElectronicTicket> builder)
        {
            builder.ToTable("ElectronicTickets");
            builder.HasKey(ticket => ticket.Id);
            builder.Property(ticket => ticket.Id).ValueGeneratedNever();
            builder.Property(ticket => ticket.DocumentNumber).HasMaxLength(32).IsRequired();
            builder.Property(ticket => ticket.ProviderReference).HasMaxLength(128);

            builder.HasIndex(ticket => ticket.DocumentNumber).IsUnique();
            builder.HasIndex(ticket => ticket.OriginalOrderId);
            builder.HasIndex(ticket => ticket.CurrentServicingOrderId);
            builder.HasIndex(ticket => new { ticket.OperationId, ticket.TravelerId }).IsUnique();

            builder.HasMany(ticket => ticket.Coupons)
                .WithOne()
                .HasForeignKey(coupon => coupon.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(ticket => ticket.PriceLinks)
                .WithOne()
                .HasForeignKey(link => link.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(ticket => ticket.Coupons).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(ticket => ticket.PriceLinks).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public sealed class TicketCouponConfiguration : IEntityTypeConfiguration<TicketCoupon>
    {
        public void Configure(EntityTypeBuilder<TicketCoupon> builder)
        {
            builder.ToTable("TicketCoupons");
            builder.HasKey(coupon => coupon.Id);
            builder.Property(coupon => coupon.Id).ValueGeneratedNever();
            builder.Property(coupon => coupon.FareBasisSnapshot).HasMaxLength(32);
            builder.Property(coupon => coupon.ProviderCouponStatusCode).HasMaxLength(16);

            builder.HasIndex(coupon => new { coupon.TicketId, coupon.CouponNumber }).IsUnique();
            builder.HasIndex(coupon => coupon.CurrentOrderServiceId);

            builder.OwnsOne(coupon => coupon.IssuedSegment, segment =>
            {
                segment.Property(value => value.FlightNumber).HasColumnName("IssuedFlightNumber").HasMaxLength(16);
                segment.Property(value => value.MarketingAirlineId).HasColumnName("IssuedMarketingAirlineId");
                segment.Property(value => value.OriginAirportId).HasColumnName("IssuedOriginAirportId");
                segment.Property(value => value.DestinationAirportId).HasColumnName("IssuedDestinationAirportId");
                segment.Property(value => value.DepartureDateTime).HasColumnName("IssuedDepartureDateTime");
                segment.Property(value => value.ArrivalDateTime).HasColumnName("IssuedArrivalDateTime");
                segment.Property(value => value.BookingClass).HasColumnName("IssuedBookingClass").HasMaxLength(8);
            });

            builder.Navigation(coupon => coupon.IssuedSegment).IsRequired();
        }
    }

    public sealed class DocumentPriceLinkConfiguration : IEntityTypeConfiguration<DocumentPriceLink>
    {
        public void Configure(EntityTypeBuilder<DocumentPriceLink> builder)
        {
            builder.ToTable("DocumentPriceLinks");
            builder.HasKey(link => link.Id);
            builder.Property(link => link.Id).ValueGeneratedNever();

            builder.HasIndex(link => new { link.TicketId, link.CouponId });
            builder.HasIndex(link => link.PricingLineId);
        }
    }
}
