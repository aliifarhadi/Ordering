using AeroTech.Ordering.Domain.PaymentAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.PaymentAggregate
{
    public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments", "Payment");
            builder.HasKey(payment => payment.Id);
            builder.Property(payment => payment.Id).ValueGeneratedNever();

            builder.Property(payment => payment.IdempotencyKey).HasMaxLength(200);
            builder.Property(payment => payment.ProviderReference).HasMaxLength(200);
            builder.Property(payment => payment.WalletReference).HasMaxLength(200);

            builder.HasIndex(payment => payment.OrderId);
            builder.HasIndex(payment => payment.IdempotencyKey);
        }
    }
}
