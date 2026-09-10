using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.ElectronicTicketAggregate
{
    public sealed class DocumentExchangeCouponConfiguration : IEntityTypeConfiguration<DocumentExchangeCoupon>
    {
        public void Configure(EntityTypeBuilder<DocumentExchangeCoupon> builder)
        {
            builder.ToTable("DocumentExchangeCoupons");
            builder.HasKey(coupon => coupon.Id);
            builder.Property(coupon => coupon.Id).ValueGeneratedNever();

            builder.HasIndex(coupon => new { coupon.DocumentExchangeRecordId, coupon.PredecessorTicketCouponId })
                .IsUnique();
            builder.HasIndex(coupon => coupon.SuccessorTicketCouponId).IsUnique();
            builder.HasIndex(coupon => coupon.PreviousOrderServiceId);
            builder.HasIndex(coupon => coupon.ReplacementOrderServiceId);
        }
    }
}
