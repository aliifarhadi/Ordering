using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderContactPointConfiguration : IEntityTypeConfiguration<OrderContactPoint>
    {
        public void Configure(EntityTypeBuilder<OrderContactPoint> builder)
        {
            builder.ToTable("OrderContactPoints");
            builder.HasKey(point => point.Id);
            builder.Property(point => point.Id).ValueGeneratedNever();
            builder.Property(point => point.Value).HasMaxLength(256);
            builder.Property(point => point.CountryCode).HasMaxLength(8);
        }
    }
}
