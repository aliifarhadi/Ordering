using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderFareComponentServiceConfiguration : IEntityTypeConfiguration<OrderFareComponentService>
    {
        public void Configure(EntityTypeBuilder<OrderFareComponentService> builder)
        {
            builder.ToTable("OrderFareComponentServices");
            builder.HasKey(service => service.Id);
            builder.Property(service => service.Id).ValueGeneratedNever();

            builder.HasIndex(service => new { service.FareComponentId, service.OrderServiceId }).IsUnique();
            builder.HasIndex(service => service.OrderServiceId);
        }
    }
}
