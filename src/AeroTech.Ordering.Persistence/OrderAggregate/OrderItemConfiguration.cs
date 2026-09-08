using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
    {
        public void Configure(EntityTypeBuilder<OrderItem> builder)
        {
            builder.ToTable("OrderItems");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.Property(item => item.ProductCode).HasMaxLength(256);
            builder.Property(item => item.ProductName).HasMaxLength(256);

            builder.HasOne(item => item.PolicySnapshot).WithOne().HasForeignKey<OrderItemPolicySnapshot>(policy => policy.OrderItemId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(item => item.PolicySnapshot).IsRequired();

            builder.HasOne(item => item.ProductSnapshot).WithOne().HasForeignKey<OrderItemProductSnapshot>(snapshot => snapshot.OrderItemId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(item => item.ProductSnapshot).IsRequired();

            builder.HasOne(item => item.CommercialTermsSnapshot).WithOne().HasForeignKey<OrderItemCommercialTermsSnapshot>(snapshot => snapshot.OrderItemId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(item => item.CommercialTermsSnapshot).IsRequired();
        }
    }
}
