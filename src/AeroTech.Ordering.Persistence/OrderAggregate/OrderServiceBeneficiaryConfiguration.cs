using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderServiceBeneficiaryConfiguration : IEntityTypeConfiguration<OrderServiceBeneficiary>
    {
        public void Configure(EntityTypeBuilder<OrderServiceBeneficiary> builder)
        {
            builder.ToTable("OrderServiceBeneficiaries");
            builder.HasKey(beneficiary => beneficiary.Id);
            builder.Property(beneficiary => beneficiary.Id).ValueGeneratedNever();

            builder.HasIndex(beneficiary => new { beneficiary.OrderServiceId, beneficiary.OrderTravellerId }).IsUnique();
            builder.HasIndex(beneficiary => beneficiary.OrderTravellerId);
        }
    }
}
