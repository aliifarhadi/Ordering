using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderHotelServiceDetailConfiguration : IEntityTypeConfiguration<OrderHotelServiceDetail>
    {
        public void Configure(EntityTypeBuilder<OrderHotelServiceDetail> builder)
        {
            builder.ToTable("OrderHotelServiceDetails");
            builder.HasKey(detail => detail.Id);
            builder.Property(detail => detail.Id).ValueGeneratedNever();
            builder.Property(detail => detail.PropertyReference).HasMaxLength(128).IsRequired();
            builder.Property(detail => detail.SupplierReference).HasMaxLength(128);
            builder.Property(detail => detail.RoomTypeCode).HasMaxLength(64);
            builder.Property(detail => detail.RatePlanReference).HasMaxLength(128);

            builder.HasIndex(detail => detail.OrderServiceId).IsUnique();
        }
    }
}
