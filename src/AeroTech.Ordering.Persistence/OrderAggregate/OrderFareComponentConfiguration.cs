using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderFareComponentConfiguration : IEntityTypeConfiguration<OrderFareComponent>
    {
        public void Configure(EntityTypeBuilder<OrderFareComponent> builder)
        {
            builder.ToTable("OrderFareComponents");
            builder.HasKey(component => component.Id);
            builder.Property(component => component.Id).ValueGeneratedNever();
            builder.Property(component => component.FareBasis).HasMaxLength(64);
            builder.Property(component => component.BrandCode).HasMaxLength(64);
            builder.Property(component => component.BrandName).HasMaxLength(128);
            builder.Property(component => component.FareType).HasMaxLength(64);
            builder.Property(component => component.BookingClass).HasMaxLength(16);
            builder.Property(component => component.TariffReference).HasMaxLength(128);
            builder.Property(component => component.RuleReference).HasMaxLength(128);
            builder.Property(component => component.RoutingReference).HasMaxLength(128);
            builder.Property(component => component.SourceFareReference).HasMaxLength(128);
            builder.Property(component => component.SourceComponentReference).HasMaxLength(128);

            builder.HasMany(component => component.Services)
                .WithOne()
                .HasForeignKey(service => service.FareComponentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(component => component.Segments)
                .WithOne()
                .HasForeignKey(segment => segment.FareComponentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(component => component.Services).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(component => component.Segments).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
