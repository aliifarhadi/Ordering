using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderGenericServiceDetailConfiguration : IEntityTypeConfiguration<OrderGenericServiceDetail>
    {
        public void Configure(EntityTypeBuilder<OrderGenericServiceDetail> builder)
        {
            builder.ToTable("OrderGenericServiceDetails");
            builder.HasKey(detail => detail.Id);
            builder.Property(detail => detail.Id).ValueGeneratedNever();
            builder.Property(detail => detail.SchemaName).HasMaxLength(64).IsRequired();
            builder.Property(detail => detail.SchemaVersion).HasMaxLength(16).IsRequired();
            builder.Property(detail => detail.AttributesJson).IsRequired();

            builder.HasIndex(detail => detail.OrderServiceId).IsUnique();
        }
    }
}
