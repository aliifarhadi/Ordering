using AeroTech.Ordering.Query.OrderAggregate.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Query.OrderAggregate.Configurations
{
    public sealed class OrderDetailsReadModelConfiguration : IEntityTypeConfiguration<OrderDetailsReadModel>
    {
        public void Configure(EntityTypeBuilder<OrderDetailsReadModel> builder)
        {
            builder.ToTable("OrderDetails");
            builder.HasKey(details => details.Id);
            builder.Property(details => details.Id).ValueGeneratedNever();
            builder.Property(details => details.SnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        }
    }
}
