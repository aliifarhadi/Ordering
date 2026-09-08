using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderAirFareConstructionItemConfiguration : IEntityTypeConfiguration<OrderAirFareConstructionItem>
    {
        public void Configure(EntityTypeBuilder<OrderAirFareConstructionItem> builder)
        {
            builder.ToTable("OrderAirFareConstructionItems");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();

            builder.HasIndex(item => new { item.FareConstructionId, item.OrderItemId }).IsUnique();
            builder.HasIndex(item => item.OrderItemId);
        }
    }
}
