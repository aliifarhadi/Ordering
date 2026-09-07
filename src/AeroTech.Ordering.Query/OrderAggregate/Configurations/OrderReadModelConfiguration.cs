using AeroTech.Ordering.Query.OrderAggregate.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Query.OrderAggregate.Configurations
{
    public sealed class OrderReadModelConfiguration : IEntityTypeConfiguration<OrderReadModel>
    {
        public void Configure(EntityTypeBuilder<OrderReadModel> builder)
        {
            builder.ToTable("Orders");
            builder.HasKey(order => order.Id);
            builder.Property(order => order.Id).ValueGeneratedNever();
            builder.Property(order => order.RecordLocator).HasMaxLength(20);
            builder.Property(order => order.LinkedPNR).HasMaxLength(20);
            builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(50);
            builder.Property(order => order.Type).HasConversion<string>().HasMaxLength(50);
            builder.Property(order => order.Channel).HasConversion<string>().HasMaxLength(50);
            builder.HasIndex(order => order.CustomerId);
            builder.HasIndex(order => order.CreationDate);
            builder.HasIndex(order => order.Status);
        }
    }
}
