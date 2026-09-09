using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Operations
{
    public sealed class AcceptedChangePlanConfiguration : IEntityTypeConfiguration<AcceptedChangePlanRow>
    {
        public void Configure(EntityTypeBuilder<AcceptedChangePlanRow> builder)
        {
            builder.ToTable("AcceptedChangePlans");
            builder.HasKey(plan => plan.OperationId);
            builder.Property(plan => plan.OperationId).ValueGeneratedNever();
            builder.Property(plan => plan.QuotedChangeId).HasMaxLength(128).IsRequired();
            builder.Property(plan => plan.SourceSystem).HasMaxLength(64).IsRequired();
            builder.Property(plan => plan.TargetSelectionRef).HasMaxLength(128).IsRequired();
            builder.Property(plan => plan.AcceptedPlan).HasColumnType("nvarchar(max)").IsRequired();
            builder.Property(plan => plan.ReservationExternalRef).HasMaxLength(128);
            builder.Property(plan => plan.EligibilityDetail).HasMaxLength(512);

            builder.HasIndex(plan => plan.OrderId);
        }
    }
}
