using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderServiceConfiguration : IEntityTypeConfiguration<OrderService>
    {
        public void Configure(EntityTypeBuilder<OrderService> builder)
        {
            builder.ToTable("OrderServices");
            builder.HasKey(service => service.Id);
            builder.Property(service => service.Id).ValueGeneratedNever();
            builder.Property(service => service.ServiceCode).HasMaxLength(32);
            builder.Property(service => service.Name).HasMaxLength(128);
            builder.Property(service => service.SupplierCode).HasMaxLength(64);
            builder.Property(service => service.DeliveryProviderReference).HasMaxLength(128);
            builder.Property(service => service.HoldBatchId).HasMaxLength(100);
            builder.Property(service => service.SeatHoldReference).HasMaxLength(100);

            builder.HasIndex(service => service.OrderItemId);

            builder.HasMany(service => service.Beneficiaries)
                .WithOne()
                .HasForeignKey(beneficiary => beneficiary.OrderServiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(service => service.CoveredServices)
                .WithOne()
                .HasForeignKey(covered => covered.OrderServiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(service => service.CoveredSegments)
                .WithOne()
                .HasForeignKey(covered => covered.OrderServiceId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(service => service.AirTransportDetail).WithOne()
                .HasForeignKey<OrderAirTransportServiceDetail>(detail => detail.OrderServiceId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(service => service.SeatDetail).WithOne()
                .HasForeignKey<OrderSeatServiceDetail>(detail => detail.OrderServiceId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(service => service.BaggageDetail).WithOne()
                .HasForeignKey<OrderBaggageServiceDetail>(detail => detail.OrderServiceId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(service => service.MealDetail).WithOne()
                .HasForeignKey<OrderMealServiceDetail>(detail => detail.OrderServiceId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(service => service.LoungeDetail).WithOne()
                .HasForeignKey<OrderLoungeServiceDetail>(detail => detail.OrderServiceId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(service => service.HotelDetail).WithOne()
                .HasForeignKey<OrderHotelServiceDetail>(detail => detail.OrderServiceId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(service => service.GroundTransportDetail).WithOne()
                .HasForeignKey<OrderGroundTransportServiceDetail>(detail => detail.OrderServiceId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(service => service.GenericDetail).WithOne()
                .HasForeignKey<OrderGenericServiceDetail>(detail => detail.OrderServiceId).OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(service => service.Beneficiaries).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(service => service.CoveredServices).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(service => service.CoveredSegments).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Ignore(service => service.AttachedDetailCount);
            builder.Ignore(service => service.IsAirTransport);
            builder.Ignore(service => service.SoleBeneficiaryId);
            builder.Ignore(service => service.SoldSegmentId);
        }
    }
}
