using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanConfiguration : IEntityTypeConfiguration<AcceptedExchangePlanRow>
    {
        public void Configure(EntityTypeBuilder<AcceptedExchangePlanRow> builder)
        {
            builder.ToTable("AcceptedExchangePlans");
            builder.HasKey(plan => plan.OperationId);
            builder.Property(plan => plan.OperationId).ValueGeneratedNever();
            builder.Property(plan => plan.QuotedExchangeId).HasMaxLength(128).IsRequired();
            builder.Property(plan => plan.SourceSystem).HasMaxLength(64).IsRequired();
            builder.Property(plan => plan.TargetSelectionRef).HasMaxLength(128).IsRequired();
            builder.Property(plan => plan.SourcePricingReference).HasMaxLength(128);
            builder.Property(plan => plan.PredecessorDocumentNumber).HasMaxLength(32).IsRequired();
            builder.Property(plan => plan.AcceptedPlan).HasColumnType("nvarchar(max)").IsRequired();
            builder.Property(plan => plan.DispositionDetail).HasMaxLength(1024);
            builder.Property(plan => plan.EligibilityDetail).HasMaxLength(512);
            builder.Property(plan => plan.ReservationExternalRef).HasMaxLength(128);
            builder.Property(plan => plan.DocumentExchangeProviderReference).HasMaxLength(128);
            builder.Property(plan => plan.DocumentExchangeDetail).HasMaxLength(512);
            builder.Property(plan => plan.DocumentExchangeSuccessorEvidence).HasColumnType("nvarchar(max)");
            builder.Property(plan => plan.SuccessorDocumentNumber).HasMaxLength(32);
            builder.Property(plan => plan.FundingMethodRef).HasMaxLength(128);
            builder.Property(plan => plan.FundingGuaranteeReference).HasMaxLength(128);
            builder.Property(plan => plan.FundingGuaranteeDetail).HasMaxLength(512);
            builder.Property(plan => plan.FundingCaptureReference).HasMaxLength(128);
            builder.Property(plan => plan.FundingCaptureDetail).HasMaxLength(512);
            builder.Property(plan => plan.FundingReleaseDetail).HasMaxLength(512);
            builder.Property(plan => plan.RefundDueReference).HasMaxLength(128);
            builder.Property(plan => plan.RefundDueDetail).HasMaxLength(512);
            builder.Property(plan => plan.ResidualProviderReference).HasMaxLength(128);
            builder.Property(plan => plan.ResidualInstrumentReference).HasMaxLength(128);
            builder.Property(plan => plan.ResidualDetail).HasMaxLength(512);

            builder.HasIndex(plan => plan.OrderId);
            builder.HasIndex(plan => plan.PredecessorElectronicTicketId);
            builder.HasIndex(plan => plan.SuccessorElectronicTicketId).IsUnique();

            builder.HasMany(plan => plan.Coupons)
                .WithOne()
                .HasForeignKey(coupon => coupon.OperationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(plan => plan.AncillaryExchangeGroups)
                .WithOne()
                .HasForeignKey(group => group.OperationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(plan => plan.AncillaryCancelGroups)
                .WithOne()
                .HasForeignKey(group => group.OperationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(plan => plan.FeeDocuments)
                .WithOne()
                .HasForeignKey(document => document.OperationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
