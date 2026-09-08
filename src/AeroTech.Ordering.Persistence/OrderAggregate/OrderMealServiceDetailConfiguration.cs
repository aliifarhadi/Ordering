using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderMealServiceDetailConfiguration : IEntityTypeConfiguration<OrderMealServiceDetail>
    {
        public void Configure(EntityTypeBuilder<OrderMealServiceDetail> builder)
        {
            builder.ToTable("OrderMealServiceDetails");
            builder.HasKey(detail => detail.Id);
            builder.Property(detail => detail.Id).ValueGeneratedNever();
            builder.Property(detail => detail.MealCode).HasMaxLength(32).IsRequired();
            builder.Property(detail => detail.SpecialMealCode).HasMaxLength(32);

            builder.HasIndex(detail => detail.OrderServiceId).IsUnique();
        }
    }
}
