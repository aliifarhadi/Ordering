using AeroTech.Ordering.Domain.ProviderInteractionAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.ProviderInteractionAggregate
{
    public sealed class ProviderInteractionConfiguration : IEntityTypeConfiguration<ProviderInteraction>
    {
        public void Configure(EntityTypeBuilder<ProviderInteraction> builder)
        {
            builder.ToTable("ProviderInteractions", "Provider");
            builder.HasKey(interaction => interaction.Id);
            builder.Property(interaction => interaction.Id).ValueGeneratedNever();

            builder.Property(interaction => interaction.IdempotencyKey).HasMaxLength(200);
            builder.Property(interaction => interaction.CorrelationId).HasMaxLength(200);
            builder.Property(interaction => interaction.SupplierCode).HasMaxLength(50);
            builder.Property(interaction => interaction.RequestPayload).HasColumnType("nvarchar(max)");
            builder.Property(interaction => interaction.ResponsePayload).HasColumnType("nvarchar(max)");

            builder.HasIndex(interaction => interaction.FulfillmentTaskId);
            builder.HasIndex(interaction => interaction.IdempotencyKey);
        }
    }
}
