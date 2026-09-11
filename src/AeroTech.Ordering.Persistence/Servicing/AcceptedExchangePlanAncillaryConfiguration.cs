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
        }
    }
}
