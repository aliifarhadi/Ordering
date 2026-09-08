using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderItemServiceLinkConfiguration : IEntityTypeConfiguration<OrderItemServiceLink>
    {
        public void Configure(EntityTypeBuilder<OrderItemServiceLink> builder)
        {
            builder.ToTable("OrderItemServiceLinks");
            builder.HasKey(link => link.Id);
            builder.Property(link => link.Id).ValueGeneratedNever();

            builder.HasIndex(link => new { link.OrderItemId, link.OrderServiceId }).IsUnique();
            builder.HasIndex(link => link.OrderServiceId);
            builder.HasIndex(link => link.LinkedByChangeId);

            builder.HasOne<OrderItem>()
                .WithMany()
                .HasForeignKey(link => link.OrderItemId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne<OrderService>()
                .WithMany()
                .HasForeignKey(link => link.OrderServiceId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasOne<OrderChange>()
                .WithMany()
                .HasForeignKey(link => link.LinkedByChangeId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
