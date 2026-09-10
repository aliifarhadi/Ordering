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
            builder.Property(plan => plan.SuccessorDocumentNumber).HasMaxLength(32);

            builder.HasIndex(plan => plan.OrderId);
            builder.HasIndex(plan => plan.PredecessorElectronicTicketId);
            builder.HasIndex(plan => plan.SuccessorElectronicTicketId).IsUnique();

            builder.HasMany(plan => plan.Coupons)
                .WithOne()
                .HasForeignKey(coupon => coupon.OperationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
