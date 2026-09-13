using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanAncillaryCancelGroupConfiguration
        : IEntityTypeConfiguration<AcceptedExchangePlanAncillaryCancelGroupRow>
    {
        public void Configure(EntityTypeBuilder<AcceptedExchangePlanAncillaryCancelGroupRow> builder)
        {
            builder.ToTable("AcceptedExchangePlanAncillaryCancelGroups");
            builder.HasKey(group => new { group.OperationId, group.CancelGroupRef });
            builder.Property(group => group.CancelGroupRef).HasMaxLength(128);
            builder.Property(group => group.EmdDocumentNumber).HasMaxLength(32).IsRequired();
            builder.Property(group => group.EmdCouponNumbers).HasMaxLength(128).IsRequired();
            builder.Property(group => group.OrderServiceIds).HasMaxLength(512).IsRequired();
            builder.Property(group => group.CancellationReference).HasMaxLength(128).IsRequired();
            builder.Property(group => group.SourceReference).HasMaxLength(128).IsRequired();
            builder.Property(group => group.DecisionReference).HasMaxLength(128).IsRequired();
            builder.Property(group => group.VoidEligibilityDetail).HasMaxLength(512);
            builder.Property(group => group.VoidProviderReference).HasMaxLength(128);
            builder.Property(group => group.VoidDetail).HasMaxLength(512);

            builder.HasIndex(group => group.ElectronicMiscDocumentId);
        }
    }
}
