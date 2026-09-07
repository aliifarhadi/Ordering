using AeroTech.Ordering.Query.OrderAggregate.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Query.OrderAggregate.Configurations
{
    public sealed class OrderTravellerReadModelConfiguration : IEntityTypeConfiguration<OrderTravellerReadModel>
    {
        public void Configure(EntityTypeBuilder<OrderTravellerReadModel> builder)
        {
            builder.ToTable("OrderTravellers");
            builder.HasKey(traveller => traveller.Id);
            builder.Property(traveller => traveller.Id).ValueGeneratedNever();
            builder.Property(traveller => traveller.FirstName).HasMaxLength(100);
            builder.Property(traveller => traveller.SurName).HasMaxLength(100);
            builder.Property(traveller => traveller.AgeRange).HasConversion<string>().HasMaxLength(20);
            builder.HasIndex(traveller => traveller.OrderId);
            builder.HasIndex(traveller => traveller.FirstName);
            builder.HasIndex(traveller => traveller.SurName);
        }
    }
}
