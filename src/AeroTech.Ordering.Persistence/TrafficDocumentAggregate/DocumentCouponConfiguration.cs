using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.TrafficDocumentAggregate
{
    public sealed class DocumentCouponConfiguration : IEntityTypeConfiguration<DocumentCoupon>
    {
        public void Configure(EntityTypeBuilder<DocumentCoupon> builder)
        {
            builder.ToTable("DocumentCoupons", "Document");
            builder.HasKey(coupon => coupon.Id);
            builder.Property(coupon => coupon.Id).ValueGeneratedNever();

            builder.HasDiscriminator<string>("CouponType")
                .HasValue<TicketCoupon>("Ticket")
                .HasValue<EmdCoupon>("Emd");

            builder.Property(coupon => coupon.CouponUniqueCode).HasMaxLength(64);

            builder.HasIndex(coupon => coupon.TrafficDocumentId);
            builder.HasIndex(coupon => coupon.OrderServiceId);
        }
    }
}
