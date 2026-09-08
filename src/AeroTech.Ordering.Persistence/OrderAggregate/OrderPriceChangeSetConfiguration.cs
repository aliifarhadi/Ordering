using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderPriceChangeSetConfiguration : IEntityTypeConfiguration<OrderPriceChangeSet>
    {
        public void Configure(EntityTypeBuilder<OrderPriceChangeSet> builder)
        {
            builder.ToTable("OrderPriceChangeSets");
            builder.HasKey(set => set.Id);
            builder.Property(set => set.Id).ValueGeneratedNever();
            builder.Property(set => set.SourceOfferId).HasMaxLength(128);
            builder.Property(set => set.SourcePricingRef).HasMaxLength(256);

            builder.HasIndex(set => new { set.OrderId, set.FinancialSequence }).IsUnique();
            builder.HasIndex(set => set.ChangeId);
        }
    }
}
