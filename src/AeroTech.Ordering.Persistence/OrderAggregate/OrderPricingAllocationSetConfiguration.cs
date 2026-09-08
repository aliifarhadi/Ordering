using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderPricingAllocationSetConfiguration : IEntityTypeConfiguration<OrderPricingAllocationSet>
    {
        public void Configure(EntityTypeBuilder<OrderPricingAllocationSet> builder)
        {
            builder.ToTable("OrderPricingAllocationSets");
            builder.HasKey(set => set.Id);
            builder.Property(set => set.Id).ValueGeneratedNever();
            builder.Property(set => set.PricingContextRef).HasMaxLength(256);
            builder.Property(set => set.PolicyVersion).HasMaxLength(64);

            builder.HasIndex(set => new { set.OrderPricingLineId, set.Purpose, set.Version }).IsUnique();

            builder.HasMany(set => set.Allocations)
                .WithOne()
                .HasForeignKey(allocation => allocation.AllocationSetId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(set => set.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
