using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.FulfillmentTaskAggregate
{
    public sealed class FulfillmentTaskAttemptConfiguration : IEntityTypeConfiguration<FulfillmentTaskAttempt>
    {
        public void Configure(EntityTypeBuilder<FulfillmentTaskAttempt> builder)
        {
            builder.ToTable("FulfillmentTaskAttempts", "Fulfillment");
            builder.HasKey(attempt => attempt.Id);
            builder.Property(attempt => attempt.Id).ValueGeneratedNever();

            builder.Property(attempt => attempt.Error).HasColumnType("nvarchar(max)");

            builder.HasIndex(attempt => attempt.ProviderInteractionId);
        }
    }
}
