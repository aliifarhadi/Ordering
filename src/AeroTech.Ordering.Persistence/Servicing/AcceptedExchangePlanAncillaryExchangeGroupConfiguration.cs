using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanAncillaryExchangeGroupConfiguration
        : IEntityTypeConfiguration<AcceptedExchangePlanAncillaryExchangeGroupRow>
    {
        public void Configure(EntityTypeBuilder<AcceptedExchangePlanAncillaryExchangeGroupRow> builder)
        {
            builder.ToTable("AcceptedExchangePlanAncillaryExchangeGroups");
            builder.HasKey(group => new { group.OperationId, group.ExchangeGroupRef });
            builder.Property(group => group.ExchangeGroupRef).HasMaxLength(128);
            builder.Property(group => group.SourceDocumentNumber).HasMaxLength(32).IsRequired();
            builder.Property(group => group.SourceCouponNumbers).HasMaxLength(128).IsRequired();
            builder.Property(group => group.SuccessorReasonForIssuanceCode).HasMaxLength(8).IsRequired();
            builder.Property(group => group.SuccessorCoupons).HasColumnType("nvarchar(max)").IsRequired();
            builder.Property(group => group.DecisionReference).HasMaxLength(128).IsRequired();
            builder.Property(group => group.SourceReference).HasMaxLength(128).IsRequired();
            builder.Property(group => group.PricingLines).HasColumnType("nvarchar(max)");
            builder.Property(group => group.AddCollectAmount).HasPrecision(18, 2);
            builder.Property(group => group.RefundDueAmount).HasPrecision(18, 2);
            builder.Property(group => group.RefundDueDisposition).HasMaxLength(64);
            builder.Property(group => group.ResidualAmount).HasPrecision(18, 2);
            builder.Property(group => group.ResidualDisposition).HasMaxLength(64);
            builder.Property(group => group.FundingMethodRef).HasMaxLength(128);
            builder.Property(group => group.ExchangeProviderReference).HasMaxLength(128);
            builder.Property(group => group.ExchangeDetail).HasMaxLength(512);
            builder.Property(group => group.SuccessorDocumentNumber).HasMaxLength(32);
            builder.Property(group => group.FundingGuaranteeReference).HasMaxLength(128);
            builder.Property(group => group.FundingGuaranteeDetail).HasMaxLength(512);
            builder.Property(group => group.FundingCaptureReference).HasMaxLength(128);
            builder.Property(group => group.FundingCaptureDetail).HasMaxLength(512);
            builder.Property(group => group.RefundDueReference).HasMaxLength(128);
            builder.Property(group => group.RefundDueDetail).HasMaxLength(512);
            builder.Property(group => group.ResidualProviderReference).HasMaxLength(128);
            builder.Property(group => group.ResidualInstrumentReference).HasMaxLength(128);
            builder.Property(group => group.ResidualDetail).HasMaxLength(512);

            builder.HasIndex(group => group.SourceElectronicMiscDocumentId);
            builder.HasIndex(group => group.SuccessorElectronicMiscDocumentId);
            builder.HasIndex(group => group.SuccessorDocumentNumber);
        }
    }
}
