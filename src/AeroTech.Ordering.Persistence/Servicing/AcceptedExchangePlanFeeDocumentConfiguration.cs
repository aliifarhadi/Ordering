using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanFeeDocumentConfiguration
        : IEntityTypeConfiguration<AcceptedExchangePlanFeeDocumentRow>
    {
        public void Configure(EntityTypeBuilder<AcceptedExchangePlanFeeDocumentRow> builder)
        {
            builder.ToTable("AcceptedExchangePlanFeeDocuments");
            builder.HasKey(document => new { document.OperationId, document.DocumentReference });
            builder.Property(document => document.DocumentReference).HasMaxLength(128);
            builder.Property(document => document.SourceReference).HasMaxLength(128).IsRequired();
            builder.Property(document => document.ReasonForIssuanceCode).HasMaxLength(8).IsRequired();
            builder.Property(document => document.TotalAmount).HasPrecision(18, 2);
            builder.Property(document => document.Coupons).HasColumnType("nvarchar(max)").IsRequired();
            builder.Property(document => document.AllocatedDocumentNumber).HasMaxLength(32);
            builder.Property(document => document.IssuanceProviderReference).HasMaxLength(128);
            builder.Property(document => document.IssuanceDetail).HasMaxLength(512);

            builder.HasIndex(document => document.AllocatedDocumentNumber);
            builder.HasIndex(document => document.ElectronicMiscDocumentId);
        }
    }
}
