using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class ServicingManualResolutionConfiguration
        : IEntityTypeConfiguration<ServicingManualResolutionRow>
    {
        public void Configure(EntityTypeBuilder<ServicingManualResolutionRow> builder)
        {
            builder.ToTable("ServicingManualResolutions");
            builder.HasKey(resolution => new { resolution.OperationId, resolution.ResolutionId });

            builder.Property(resolution => resolution.ResolutionId).HasMaxLength(128).IsRequired();
            builder.Property(resolution => resolution.Actor).HasMaxLength(128).IsRequired();
            builder.Property(resolution => resolution.Reason).HasMaxLength(512).IsRequired();
            builder.Property(resolution => resolution.Reference).HasMaxLength(128);
        }
    }
}
