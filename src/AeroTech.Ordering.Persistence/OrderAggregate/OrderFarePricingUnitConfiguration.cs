using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderFarePricingUnitConfiguration : IEntityTypeConfiguration<OrderFarePricingUnit>
    {
        public void Configure(EntityTypeBuilder<OrderFarePricingUnit> builder)
        {
            builder.ToTable("OrderFarePricingUnits");
            builder.HasKey(unit => unit.Id);
            builder.Property(unit => unit.Id).ValueGeneratedNever();
            builder.Property(unit => unit.SourceReference).HasMaxLength(128);

            builder.HasMany(unit => unit.FareComponents)
                .WithOne()
                .HasForeignKey(component => component.PricingUnitId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(unit => unit.FareComponents).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
