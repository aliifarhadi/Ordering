using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanAncillaryConfiguration
        : IEntityTypeConfiguration<AcceptedExchangePlanAncillaryRow>
    {
        public void Configure(EntityTypeBuilder<AcceptedExchangePlanAncillaryRow> builder)
        {
            builder.ToTable("AcceptedExchangePlanAncillaries");
            builder.HasKey(ancillary => new { ancillary.OperationId, ancillary.EmdCouponId });
            builder.Property(ancillary => ancillary.EmdDocumentNumber).HasMaxLength(32).IsRequired();
            builder.Property(ancillary => ancillary.PredecessorDocumentNumber).HasMaxLength(32).IsRequired();
            builder.Property(ancillary => ancillary.DecisionReference).HasMaxLength(128).IsRequired();
            builder.Property(ancillary => ancillary.DecisionContextFingerprint).HasMaxLength(64).IsRequired();
            builder.Property(ancillary => ancillary.AssociationProviderReference).HasMaxLength(128);
            builder.Property(ancillary => ancillary.AssociationDetail).HasMaxLength(512);
            builder.Property(ancillary => ancillary.RefundAmount).HasPrecision(18, 2);
            builder.Property(ancillary => ancillary.RefundDisposition).HasMaxLength(64);
            builder.Property(ancillary => ancillary.RefundSourceReference).HasMaxLength(128);
            builder.Property(ancillary => ancillary.RefundPricingLines).HasColumnType("nvarchar(max)");
            builder.Property(ancillary => ancillary.RefundDocumentReference).HasMaxLength(128);
            builder.Property(ancillary => ancillary.RefundDocumentDetail).HasMaxLength(512);
            builder.Property(ancillary => ancillary.RefundValueReference).HasMaxLength(128);
            builder.Property(ancillary => ancillary.RefundValueDetail).HasMaxLength(512);
            builder.Property(ancillary => ancillary.ExchangeGroupRef).HasMaxLength(128);
            builder.Property(ancillary => ancillary.RetentionReference).HasMaxLength(128);
            builder.Property(ancillary => ancillary.RetentionSourceReference).HasMaxLength(128);

            builder.HasIndex(ancillary => new { ancillary.OperationId, ancillary.ExchangeGroupRef });
        }
    }
}
