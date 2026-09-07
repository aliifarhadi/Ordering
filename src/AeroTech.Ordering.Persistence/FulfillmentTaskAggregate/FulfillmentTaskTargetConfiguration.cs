using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.FulfillmentTaskAggregate
{
    public sealed class FulfillmentTaskTargetConfiguration : IEntityTypeConfiguration<FulfillmentTaskTarget>
    {
        public void Configure(EntityTypeBuilder<FulfillmentTaskTarget> builder)
        {
            builder.ToTable("FulfillmentTaskTargets", "Fulfillment");
            builder.HasKey(target => target.Id);
            builder.Property(target => target.Id).ValueGeneratedNever();

            builder.Property(target => target.FulfillmentReference).HasMaxLength(100);
            builder.Property(target => target.ServiceReference).HasMaxLength(100);

            builder.HasIndex(target => target.OrderServiceId);
        }
    }
}
