using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderLoungeServiceDetailConfiguration : IEntityTypeConfiguration<OrderLoungeServiceDetail>
    {
        public void Configure(EntityTypeBuilder<OrderLoungeServiceDetail> builder)
        {
            builder.ToTable("OrderLoungeServiceDetails");
            builder.HasKey(detail => detail.Id);
            builder.Property(detail => detail.Id).ValueGeneratedNever();
            builder.Property(detail => detail.LoungeCode).HasMaxLength(32);

            builder.HasIndex(detail => detail.OrderServiceId).IsUnique();
            builder.HasIndex(detail => detail.RelatedAirOrderServiceId);

            builder.HasOne<OrderService>()
                .WithMany()
                .HasForeignKey(detail => detail.RelatedAirOrderServiceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
