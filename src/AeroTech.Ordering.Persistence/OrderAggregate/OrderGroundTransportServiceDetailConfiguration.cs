using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderGroundTransportServiceDetailConfiguration : IEntityTypeConfiguration<OrderGroundTransportServiceDetail>
    {
        public void Configure(EntityTypeBuilder<OrderGroundTransportServiceDetail> builder)
        {
            builder.ToTable("OrderGroundTransportServiceDetails");
            builder.HasKey(detail => detail.Id);
            builder.Property(detail => detail.Id).ValueGeneratedNever();
            builder.Property(detail => detail.PickupLocationReference).HasMaxLength(128).IsRequired();
            builder.Property(detail => detail.DropoffLocationReference).HasMaxLength(128).IsRequired();
            builder.Property(detail => detail.VehicleTypeCode).HasMaxLength(64);

            builder.HasIndex(detail => detail.OrderServiceId).IsUnique();
        }
    }
}
