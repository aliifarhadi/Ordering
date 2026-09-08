using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderItemProductSnapshotConfiguration : IEntityTypeConfiguration<OrderItemProductSnapshot>
    {
        public void Configure(EntityTypeBuilder<OrderItemProductSnapshot> builder)
        {
            builder.ToTable("OrderItemProductSnapshots");
            builder.HasKey(snapshot => snapshot.Id);
            builder.Property(snapshot => snapshot.Id).ValueGeneratedNever();
            builder.Property(snapshot => snapshot.SourceProductReference).HasMaxLength(128).IsRequired();
            builder.Property(snapshot => snapshot.ProductCode).HasMaxLength(128);
            builder.Property(snapshot => snapshot.ProductName).HasMaxLength(256);
            builder.Property(snapshot => snapshot.Brand).HasMaxLength(128);
            builder.Property(snapshot => snapshot.SupplierCode).HasMaxLength(64);
            builder.Property(snapshot => snapshot.SourceSystem).HasMaxLength(64).IsRequired();
            builder.Property(snapshot => snapshot.SourceOfferId).HasMaxLength(128).IsRequired();
            builder.Property(snapshot => snapshot.SourcePricingReference).HasMaxLength(128);
        }
    }
}
