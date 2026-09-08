using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderAirFareConstructionConfiguration : IEntityTypeConfiguration<OrderAirFareConstruction>
    {
        public void Configure(EntityTypeBuilder<OrderAirFareConstruction> builder)
        {
            builder.ToTable("OrderAirFareConstructions");
            builder.Ignore(construction => construction.FareComponents);
            builder.HasKey(construction => construction.Id);
            builder.Property(construction => construction.Id).ValueGeneratedNever();
            builder.Property(construction => construction.SourceSystem).HasMaxLength(64).IsRequired();
            builder.Property(construction => construction.SourcePricingReference).HasMaxLength(128);

            builder.HasIndex(construction => construction.OrderId);
            builder.HasIndex(construction => construction.SupersedesConstructionId);

            builder.HasMany(construction => construction.Items)
                .WithOne()
                .HasForeignKey(item => item.FareConstructionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(construction => construction.PricingGroups)
                .WithOne()
                .HasForeignKey(group => group.FareConstructionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(construction => construction.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(construction => construction.PricingGroups).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
