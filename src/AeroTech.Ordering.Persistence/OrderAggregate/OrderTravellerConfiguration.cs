using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderTravellerConfiguration : IEntityTypeConfiguration<OrderTraveller>
    {
        public void Configure(EntityTypeBuilder<OrderTraveller> builder)
        {
            builder.ToTable("OrderTravellers");
            builder.HasKey(traveller => traveller.Id);
            builder.Property(traveller => traveller.Id).ValueGeneratedNever();

            builder.OwnsOne(traveller => traveller.Name, name =>
            {
                name.Property(value => value.FirstName).HasColumnName("FirstName").HasMaxLength(30);
                name.Property(value => value.SurName).HasColumnName("SurName").HasMaxLength(30);
                name.Property(value => value.NoSurname).HasColumnName("NoSurname");
            });
            builder.Navigation(traveller => traveller.Name).IsRequired();

            builder.HasMany(traveller => traveller.Documents).WithOne().HasForeignKey("OrderTravellerId").OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(traveller => traveller.Documents).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
