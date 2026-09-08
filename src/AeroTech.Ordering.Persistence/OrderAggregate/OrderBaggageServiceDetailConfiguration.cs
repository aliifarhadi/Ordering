using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderBaggageServiceDetailConfiguration : IEntityTypeConfiguration<OrderBaggageServiceDetail>
    {
        public void Configure(EntityTypeBuilder<OrderBaggageServiceDetail> builder)
        {
            builder.ToTable("OrderBaggageServiceDetails");
            builder.HasKey(detail => detail.Id);
            builder.Property(detail => detail.Id).ValueGeneratedNever();
            builder.Property(detail => detail.Weight).HasPrecision(18, 3);
            builder.Property(detail => detail.PerPieceWeightLimit).HasPrecision(18, 3);

            builder.HasIndex(detail => detail.OrderServiceId).IsUnique();
            builder.Ignore(detail => detail.HasQuantity);
        }
    }
}
