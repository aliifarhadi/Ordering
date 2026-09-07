using AeroTech.Ordering.Domain.OrderAggregate.Constants;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderRemarkConfiguration : IEntityTypeConfiguration<OrderRemark>
    {
        public void Configure(EntityTypeBuilder<OrderRemark> builder)
        {
            builder.ToTable("OrderRemarks");
            builder.HasKey(remark => remark.Id);
            builder.Property(remark => remark.Id).ValueGeneratedNever();

            builder.Property(remark => remark.Text).HasMaxLength(OrderRemarkRules.MaxTextLength).IsRequired();
            builder.Property(remark => remark.CategoryCode).HasMaxLength(OrderRemarkRules.MaxCategoryCodeLength);

            builder.HasIndex(remark => new { remark.OrderId, remark.Status });
            builder.HasIndex(remark => new { remark.OrderId, remark.Type });
        }
    }
}
