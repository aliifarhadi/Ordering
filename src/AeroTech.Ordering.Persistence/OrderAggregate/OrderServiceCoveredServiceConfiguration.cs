using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderServiceCoveredServiceConfiguration : IEntityTypeConfiguration<OrderServiceCoveredService>
    {
        public void Configure(EntityTypeBuilder<OrderServiceCoveredService> builder)
        {
            builder.ToTable("OrderServiceCoveredServices");
            builder.HasKey(covered => covered.Id);
            builder.Property(covered => covered.Id).ValueGeneratedNever();

            builder.HasIndex(covered => new { covered.OrderServiceId, covered.CoveredOrderServiceId }).IsUnique();
            builder.HasIndex(covered => covered.CoveredOrderServiceId);
        }
    }
}
