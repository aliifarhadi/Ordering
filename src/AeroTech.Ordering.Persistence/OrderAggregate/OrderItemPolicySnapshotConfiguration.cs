using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderItemPolicySnapshotConfiguration : IEntityTypeConfiguration<OrderItemPolicySnapshot>
    {
        public void Configure(EntityTypeBuilder<OrderItemPolicySnapshot> builder)
        {
            builder.ToTable("OrderItemPolicySnapshots");
            builder.HasKey(policy => policy.Id);
            builder.Property(policy => policy.Id).ValueGeneratedNever();
            builder.Property(policy => policy.SnapshotVersion).HasMaxLength(256);
        }
    }
}
