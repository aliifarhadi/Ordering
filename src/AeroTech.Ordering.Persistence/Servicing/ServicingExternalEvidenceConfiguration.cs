using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class ServicingExternalEvidenceConfiguration
        : IEntityTypeConfiguration<ServicingExternalEvidenceRow>
    {
        public void Configure(EntityTypeBuilder<ServicingExternalEvidenceRow> builder)
        {
            builder.ToTable("ServicingExternalEvidences");
            builder.HasKey(evidence => new { evidence.OperationId, evidence.Stage });
            builder.Property(evidence => evidence.ProviderReference).HasMaxLength(128);
            builder.Property(evidence => evidence.Detail).HasMaxLength(512);
            builder.Property(evidence => evidence.DocumentNumber).HasMaxLength(32);

            builder.HasIndex(evidence => evidence.DocumentNumber);
        }
    }
}
