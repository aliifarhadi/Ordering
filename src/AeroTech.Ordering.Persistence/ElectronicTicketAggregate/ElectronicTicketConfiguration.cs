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

            builder.HasMany(ticket => ticket.Refunds)
                .WithOne()
                .HasForeignKey(record => record.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(ticket => ticket.RefundCorrections)
                .WithOne()
                .HasForeignKey(record => record.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(ticket => ticket.Revalidations)
                .WithOne()
                .HasForeignKey(record => record.TicketId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.OwnsOne(ticket => ticket.VoidRecord, record =>
            {
                record.Property(value => value.OperationId).HasColumnName("VoidOperationId");
                record.Property(value => value.Reason).HasColumnName("VoidReason");
                record.Property(value => value.ReasonDetail).HasColumnName("VoidReasonDetail").HasMaxLength(512);
                record.Property(value => value.VoidedBy).HasColumnName("VoidedBy");
                record.Property(value => value.VoidedAt).HasColumnName("VoidedAt");
                record.Property(value => value.ProviderReference).HasColumnName("VoidProviderReference").HasMaxLength(128);
            });

            builder.Navigation(ticket => ticket.Coupons).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(ticket => ticket.PriceLinks).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(ticket => ticket.Refunds).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(ticket => ticket.RefundCorrections).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(ticket => ticket.Revalidations).UsePropertyAccessMode(PropertyAccessMode.Field);
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

    public sealed class DocumentRefundRecordConfiguration : IEntityTypeConfiguration<DocumentRefundRecord>
    {
        public void Configure(EntityTypeBuilder<DocumentRefundRecord> builder)
        {
            builder.ToTable("DocumentRefundRecords");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Id).ValueGeneratedNever();
            builder.Property(record => record.QuotedRefundId).HasMaxLength(128).IsRequired();
            builder.Property(record => record.SourcePricingReference).HasMaxLength(128);
            builder.Property(record => record.SourceRefundType).HasMaxLength(64);
            builder.Property(record => record.SourceEvidence).HasMaxLength(4000);
            builder.Property(record => record.ApprovedDisposition).HasMaxLength(64).IsRequired();
            builder.Property(record => record.DispositionReference).HasMaxLength(128);
            builder.Property(record => record.ProviderReference).HasMaxLength(128);
            builder.Property(record => record.ActorScope).HasMaxLength(128);
            builder.Property(record => record.ValueMovementReference).HasMaxLength(128);
            builder.Property(record => record.ValueMovementDetail).HasMaxLength(512);

            builder.HasIndex(record => new { record.TicketId, record.OperationId }).IsUnique();
            builder.HasIndex(record => record.OperationId);

            builder.OwnsOne(record => record.ManualAuthority, authority =>
            {
                authority.Property(value => value.Reference)
                    .HasColumnName("ManualAuthorityReference").HasMaxLength(128);
                authority.Property(value => value.Reason)
                    .HasColumnName("ManualRefundReason").HasMaxLength(512);
            });

            builder.HasMany(record => record.Coupons)
                .WithOne()
                .HasForeignKey(coupon => coupon.DocumentRefundRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(record => record.Coupons).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public sealed class DocumentRevalidationRecordConfiguration : IEntityTypeConfiguration<DocumentRevalidationRecord>
    {
        public void Configure(EntityTypeBuilder<DocumentRevalidationRecord> builder)
        {
            builder.ToTable("DocumentRevalidationRecords");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Id).ValueGeneratedNever();
            builder.Property(record => record.QuotedChangeId).HasMaxLength(128).IsRequired();
            builder.Property(record => record.TargetSelectionRef).HasMaxLength(128).IsRequired();
            builder.Property(record => record.ProviderReference).HasMaxLength(128);
            builder.Property(record => record.ActorScope).HasMaxLength(128);

            builder.HasIndex(record => new { record.TicketId, record.OperationId }).IsUnique();
            builder.HasIndex(record => record.TicketCouponId);
        }
    }

    public sealed class DocumentRefundCorrectionRecordConfiguration
        : IEntityTypeConfiguration<DocumentRefundCorrectionRecord>
    {
        public void Configure(EntityTypeBuilder<DocumentRefundCorrectionRecord> builder)
        {
            builder.ToTable("DocumentRefundCorrectionRecords");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Id).ValueGeneratedNever();
            builder.Property(record => record.Reason).HasMaxLength(128).IsRequired();
            builder.Property(record => record.ReasonDetail).HasMaxLength(512);
            builder.Property(record => record.ProviderReference).HasMaxLength(128);
            builder.Property(record => record.ActorScope).HasMaxLength(128);
            builder.Property(record => record.ValueCorrectionReference).HasMaxLength(128);
            builder.Property(record => record.ValueCorrectionDetail).HasMaxLength(512);

            builder.HasIndex(record => record.DocumentRefundRecordId).IsUnique();
            builder.HasIndex(record => new { record.TicketId, record.OperationId }).IsUnique();
            builder.HasIndex(record => record.OperationId);

            builder.HasMany(record => record.Coupons)
                .WithOne()
                .HasForeignKey(coupon => coupon.DocumentRefundCorrectionRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(record => record.Coupons).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public sealed class DocumentRefundCorrectionCouponConfiguration
        : IEntityTypeConfiguration<DocumentRefundCorrectionCoupon>
    {
        public void Configure(EntityTypeBuilder<DocumentRefundCorrectionCoupon> builder)
        {
            builder.ToTable("DocumentRefundCorrectionCoupons");
            builder.HasKey(coupon => coupon.Id);
            builder.Property(coupon => coupon.Id).ValueGeneratedNever();

            builder.HasIndex(coupon => new { coupon.DocumentRefundCorrectionRecordId, coupon.TicketCouponId })
                .IsUnique();
            builder.HasIndex(coupon => coupon.OrderServiceId);
        }
    }

    public sealed class DocumentRefundCouponConfiguration : IEntityTypeConfiguration<DocumentRefundCoupon>
    {
        public void Configure(EntityTypeBuilder<DocumentRefundCoupon> builder)
        {
            builder.ToTable("DocumentRefundCoupons");
            builder.HasKey(coupon => coupon.Id);
            builder.Property(coupon => coupon.Id).ValueGeneratedNever();

            builder.HasIndex(coupon => new { coupon.DocumentRefundRecordId, coupon.TicketCouponId }).IsUnique();
            builder.HasIndex(coupon => coupon.OrderServiceId);
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
