using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderChangeConfiguration : IEntityTypeConfiguration<OrderChange>
    {
        public void Configure(EntityTypeBuilder<OrderChange> builder)
        {
            builder.ToTable("OrderChanges");
            builder.HasKey(change => change.Id);
            builder.Property(change => change.Id).ValueGeneratedNever();
            builder.Property(change => change.Reason).HasMaxLength(256);
            builder.Property(change => change.ExternalReference).HasMaxLength(256);
            builder.Property(change => change.ActorScope).HasMaxLength(512);

            builder.HasIndex(change => new { change.OrderId, change.OccurredAt });
        }
    }
}
