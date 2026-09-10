using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.ElectronicTicketAggregate
{
    public sealed class DocumentExchangeRecordConfiguration : IEntityTypeConfiguration<DocumentExchangeRecord>
    {
        public void Configure(EntityTypeBuilder<DocumentExchangeRecord> builder)
        {
            builder.ToTable("DocumentExchangeRecords");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Id).ValueGeneratedNever();
            builder.Property(record => record.SuccessorDocumentNumber).HasMaxLength(32).IsRequired();
            builder.Property(record => record.QuotedExchangeId).HasMaxLength(128).IsRequired();
            builder.Property(record => record.TargetSelectionRef).HasMaxLength(128).IsRequired();
            builder.Property(record => record.SourcePricingReference).HasMaxLength(128);
            builder.Property(record => record.ProviderReference).HasMaxLength(128);
            builder.Property(record => record.ActorScope).HasMaxLength(128);

            builder.HasIndex(record => record.PredecessorElectronicTicketId).IsUnique();
            builder.HasIndex(record => record.SuccessorElectronicTicketId).IsUnique();
            builder.HasIndex(record => record.OperationId).IsUnique();

            builder.HasMany(record => record.Coupons)
                .WithOne()
                .HasForeignKey(coupon => coupon.DocumentExchangeRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(record => record.Coupons).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
