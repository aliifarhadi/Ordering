using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderSeatServiceDetailConfiguration : IEntityTypeConfiguration<OrderSeatServiceDetail>
    {
        public void Configure(EntityTypeBuilder<OrderSeatServiceDetail> builder)
        {
            builder.ToTable("OrderSeatServiceDetails");
            builder.HasKey(detail => detail.Id);
            builder.Property(detail => detail.Id).ValueGeneratedNever();
            builder.Property(detail => detail.SoldSeatNumber).HasMaxLength(16);

            builder.HasIndex(detail => detail.OrderServiceId).IsUnique();
            builder.HasIndex(detail => detail.AssociatedAirOrderServiceId);

            builder.HasOne<OrderService>()
                .WithMany()
                .HasForeignKey(detail => detail.AssociatedAirOrderServiceId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
