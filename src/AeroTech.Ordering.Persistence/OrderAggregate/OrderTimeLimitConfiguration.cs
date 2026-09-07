using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderTimeLimitConfiguration : IEntityTypeConfiguration<OrderTimeLimit>
    {
        public void Configure(EntityTypeBuilder<OrderTimeLimit> builder)
        {
            builder.ToTable("OrderTimeLimits");
            builder.HasKey(limit => limit.Id);
            builder.Property(limit => limit.Id).ValueGeneratedNever();
            builder.Property(limit => limit.SourceReference).HasMaxLength(128);
            builder.HasIndex(limit => new { limit.OrderId, limit.Type, limit.Status });
            builder.HasIndex(limit => new { limit.Status, limit.DueAt });
        }
    }

    public sealed class OrderExternalReferenceConfiguration : IEntityTypeConfiguration<OrderExternalReference>
    {
        public void Configure(EntityTypeBuilder<OrderExternalReference> builder)
        {
            builder.ToTable("OrderExternalReferences");
            builder.HasKey(reference => reference.Id);
            builder.Property(reference => reference.Id).ValueGeneratedNever();
            builder.Property(reference => reference.SourceSystem).HasMaxLength(64).IsRequired();
            builder.Property(reference => reference.Reference).HasMaxLength(256).IsRequired();
            builder.HasIndex(reference => new { reference.OrderId, reference.Type });
            builder.HasIndex(reference => new { reference.SourceSystem, reference.Reference });
        }
    }
}
