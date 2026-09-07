using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderServiceConfiguration : IEntityTypeConfiguration<OrderService>
    {
        public void Configure(EntityTypeBuilder<OrderService> builder)
        {
            builder.ToTable("OrderServices");
            builder.UseTptMappingStrategy();
            builder.HasKey(service => service.Id);
            builder.Property(service => service.Id).ValueGeneratedNever();
            builder.Property(service => service.ServiceCode).HasMaxLength(32);
            builder.Property(service => service.Name).HasMaxLength(128);
            builder.Property(service => service.SupplierCode).HasMaxLength(64);
            builder.Property(service => service.HoldBatchId).HasMaxLength(100);
            builder.Property(service => service.SeatHoldReference).HasMaxLength(100);
        }
    }
}
