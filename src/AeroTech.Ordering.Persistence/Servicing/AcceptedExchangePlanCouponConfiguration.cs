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

            builder.HasIndex(coupon => coupon.SuccessorTicketCouponId).IsUnique();
            builder.HasIndex(coupon => coupon.PredecessorOrderServiceId);
        }
    }
}
