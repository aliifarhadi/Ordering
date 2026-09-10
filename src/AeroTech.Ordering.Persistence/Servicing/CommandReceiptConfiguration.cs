using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class CommandReceiptConfiguration : IEntityTypeConfiguration<CommandReceipt>
    {
        public void Configure(EntityTypeBuilder<CommandReceipt> builder)
        {
            builder.ToTable("CommandReceipts");
            builder.HasKey(receipt => receipt.Id);
            builder.Property(receipt => receipt.Id).ValueGeneratedNever();

            builder.Property(receipt => receipt.CallerScope).HasMaxLength(256).IsRequired();
            builder.Property(receipt => receipt.OperationName).HasMaxLength(128).IsRequired();
            builder.Property(receipt => receipt.IdempotencyKey).HasMaxLength(128).IsRequired();
            builder.Property(receipt => receipt.RequestHash).HasMaxLength(128).IsRequired();
            builder.Property(receipt => receipt.PayloadRef).HasMaxLength(512);
            builder.Property(receipt => receipt.ResultRef).HasMaxLength(512);

            builder
                .HasIndex(receipt => new
                {
                    receipt.OwnerAirlineId,
                    receipt.CallerScope,
                    receipt.OperationName,
                    receipt.IdempotencyKey
                })
                .IsUnique();

            builder.HasIndex(receipt => receipt.OrderId);
        }
    }
}
