using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanCouponConfiguration : IEntityTypeConfiguration<AcceptedExchangePlanCouponRow>
    {
        public void Configure(EntityTypeBuilder<AcceptedExchangePlanCouponRow> builder)
        {
            builder.ToTable("AcceptedExchangePlanCoupons");
            builder.HasKey(coupon => new { coupon.OperationId, coupon.PredecessorTicketCouponId });

            builder.Property(coupon => coupon.SegmentFlightNumber).HasMaxLength(16).IsRequired();
            builder.Property(coupon => coupon.SegmentBookingClass).HasMaxLength(8);

            builder.HasIndex(coupon => coupon.SuccessorTicketCouponId).IsUnique();
            builder.HasIndex(coupon => coupon.PredecessorOrderServiceId);
        }
    }
}
