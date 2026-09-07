using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.ToTable("Orders");
            builder.HasKey(order => order.Id);
            builder.Property(order => order.Id).ValueGeneratedNever();

            builder.HasIndex(order => new { order.Status, order.TimeToLive });

            builder.OwnsOne(order => order.Amount, amount =>
            {
                amount.Property(value => value.BaseFareTotal).HasColumnName("BaseFareTotal");
                amount.Property(value => value.TaxTotal).HasColumnName("TaxTotal");
                amount.Property(value => value.FeeTotal).HasColumnName("FeeTotal");
                amount.Property(value => value.SurchargeTotal).HasColumnName("SurchargeTotal");
                amount.Property(value => value.DiscountTotal).HasColumnName("DiscountTotal");
                amount.Property(value => value.PenaltyTotal).HasColumnName("PenaltyTotal");
                amount.Property(value => value.GrandTotal).HasColumnName("GrandTotal");
            });
            builder.Navigation(order => order.Amount).IsRequired();

            builder.OwnsOne(order => order.Commission, commission =>
            {
                commission.Property(value => value.CommissionRate).HasColumnName("CommissionRate");
                commission.Property(value => value.CommissionAmount).HasColumnName("CommissionAmount");
            });
            builder.Navigation(order => order.Commission).IsRequired();

            builder.OwnsOne(order => order.RecordLocator, recordLocator =>
            {
                recordLocator.Property(value => value.Value).HasColumnName("RecordLocator").HasMaxLength(16);
                recordLocator.HasIndex(value => value.Value).IsUnique().HasFilter("[RecordLocator] IS NOT NULL");
            });

            builder.OwnsOne(order => order.PaymentSummary, summary =>
            {
                summary.ToTable("OrderPaymentSummaries");
                summary.Property(value => value.ProviderReference).HasColumnName("ProviderReference").HasMaxLength(200);
            });

            builder.HasOne(order => order.Contact).WithOne().HasForeignKey<OrderContact>(contact => contact.OrderId).OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(order => order.Items).WithOne().HasForeignKey(item => item.OrderId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(order => order.PricingLines).WithOne().HasForeignKey(line => line.OrderId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(order => order.Travellers).WithOne().HasForeignKey(traveller => traveller.OrderId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(order => order.Segments).WithOne().HasForeignKey(segment => segment.OrderId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(order => order.OrderServices).WithOne().HasForeignKey(service => service.OrderId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(order => order.Itineraries).WithOne().HasForeignKey(itinerary => itinerary.OrderId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(order => order.Remarks).WithOne().HasForeignKey(remark => remark.OrderId).OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(order => order.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(order => order.PricingLines).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(order => order.Travellers).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(order => order.Segments).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(order => order.OrderServices).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(order => order.Itineraries).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(order => order.Remarks).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
