using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.ElectronicMiscDocumentAggregate
{
    public sealed class ElectronicMiscDocumentConfiguration : IEntityTypeConfiguration<ElectronicMiscDocument>
    {
        public void Configure(EntityTypeBuilder<ElectronicMiscDocument> builder)
        {
            builder.ToTable("ElectronicMiscDocuments");
            builder.HasKey(document => document.Id);
            builder.Property(document => document.Id).ValueGeneratedNever();
            builder.Property(document => document.DocumentNumber).HasMaxLength(32).IsRequired();
            builder.Property(document => document.ReasonForIssuanceCode).HasMaxLength(8).IsRequired();
            builder.Property(document => document.ProviderReference).HasMaxLength(128);

            builder.HasIndex(document => document.DocumentNumber).IsUnique();
            builder.HasIndex(document => document.OriginalOrderId);
            builder.HasIndex(document => document.CurrentServicingOrderId);
            builder.HasIndex(document => document.OperationId);

            builder.HasMany(document => document.Coupons)
                .WithOne()
                .HasForeignKey(coupon => coupon.ElectronicMiscDocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(document => document.PriceLinks)
                .WithOne()
                .HasForeignKey(link => link.ElectronicMiscDocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(document => document.Coupons).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(document => document.PriceLinks).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Ignore(document => document.IsAssociated);
        }
    }

    public sealed class EmdCouponConfiguration : IEntityTypeConfiguration<EmdCoupon>
    {
        public void Configure(EntityTypeBuilder<EmdCoupon> builder)
        {
            builder.ToTable("EmdCoupons");
            builder.HasKey(coupon => coupon.Id);
            builder.Property(coupon => coupon.Id).ValueGeneratedNever();
            builder.Property(coupon => coupon.ReasonForIssuanceSubCode).HasMaxLength(8).IsRequired();
            builder.Property(coupon => coupon.ExternalValueReference).HasMaxLength(128);

            builder.HasIndex(coupon => new { coupon.ElectronicMiscDocumentId, coupon.CouponNumber }).IsUnique();
            builder.HasIndex(coupon => coupon.OrderServiceId);
            builder.HasIndex(coupon => coupon.PricingLineId);
            builder.HasIndex(coupon => coupon.AssociatedTicketCouponId);

            builder.HasOne<OrderService>()
                .WithMany()
                .HasForeignKey(coupon => coupon.OrderServiceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne<OrderPricingLine>()
                .WithMany()
                .HasForeignKey(coupon => coupon.PricingLineId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne<TicketCoupon>()
                .WithMany()
                .HasForeignKey(coupon => coupon.AssociatedTicketCouponId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }

    public sealed class EmdPriceLinkConfiguration : IEntityTypeConfiguration<EmdPriceLink>
    {
        public void Configure(EntityTypeBuilder<EmdPriceLink> builder)
        {
            builder.ToTable("EmdPriceLinks");
            builder.HasKey(link => link.Id);
            builder.Property(link => link.Id).ValueGeneratedNever();

            builder.HasIndex(link => new { link.ElectronicMiscDocumentId, link.EmdCouponId });
            builder.HasIndex(link => link.PricingLineId);
            builder.HasIndex(link => link.AllocationId);

            builder.HasOne<EmdCoupon>()
                .WithMany()
                .HasForeignKey(link => link.EmdCouponId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne<OrderPricingLine>()
                .WithMany()
                .HasForeignKey(link => link.PricingLineId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne<OrderPricingAllocation>()
                .WithMany()
                .HasForeignKey(link => link.AllocationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
