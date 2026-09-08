using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderFarePricingGroupConfiguration : IEntityTypeConfiguration<OrderFarePricingGroup>
    {
        public void Configure(EntityTypeBuilder<OrderFarePricingGroup> builder)
        {
            builder.ToTable("OrderFarePricingGroups");
            builder.HasKey(group => group.Id);
            builder.Property(group => group.Id).ValueGeneratedNever();
            builder.Property(group => group.SourceReference).HasMaxLength(128);

            builder.HasMany(group => group.Travellers)
                .WithOne()
                .HasForeignKey(traveller => traveller.PricingGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(group => group.PricingUnits)
                .WithOne()
                .HasForeignKey(unit => unit.PricingGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(group => group.Travellers).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(group => group.PricingUnits).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
