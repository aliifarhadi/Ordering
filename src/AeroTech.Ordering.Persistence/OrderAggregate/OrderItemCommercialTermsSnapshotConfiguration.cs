using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderItemCommercialTermsSnapshotConfiguration : IEntityTypeConfiguration<OrderItemCommercialTermsSnapshot>
    {
        public void Configure(EntityTypeBuilder<OrderItemCommercialTermsSnapshot> builder)
        {
            builder.ToTable("OrderItemCommercialTermsSnapshots");
            builder.HasKey(snapshot => snapshot.Id);
            builder.Property(snapshot => snapshot.Id).ValueGeneratedNever();
            builder.Property(snapshot => snapshot.SourceSystem).HasMaxLength(64).IsRequired();
            builder.Property(snapshot => snapshot.SourcePolicyReference).HasMaxLength(128);
            builder.Property(snapshot => snapshot.SourcePolicyVersion).HasMaxLength(64);
        }
    }
}
