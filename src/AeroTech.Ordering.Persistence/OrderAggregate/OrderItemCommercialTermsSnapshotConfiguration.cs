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
            builder.Property(snapshot => snapshot.PolicySource).HasMaxLength(64).IsRequired();
            builder.Property(snapshot => snapshot.SourceRuleReference).HasMaxLength(128);

            builder.OwnsOne(snapshot => snapshot.CheckedBaggage, baggage =>
            {
                baggage.Property(value => value.Weight).HasColumnName("CheckedBaggageWeight");
                baggage.Property(value => value.Unit).HasColumnName("CheckedBaggageUnit");
                baggage.Property(value => value.Pieces).HasColumnName("CheckedBaggagePieces");
            });

            builder.OwnsOne(snapshot => snapshot.CabinBaggage, baggage =>
            {
                baggage.Property(value => value.Weight).HasColumnName("CabinBaggageWeight");
                baggage.Property(value => value.Unit).HasColumnName("CabinBaggageUnit");
                baggage.Property(value => value.Pieces).HasColumnName("CabinBaggagePieces");
            });
        }
    }
}
