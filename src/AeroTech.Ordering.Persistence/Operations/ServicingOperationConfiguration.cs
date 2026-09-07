using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Operations
{
    public sealed class ServicingOperationConfiguration : IEntityTypeConfiguration<ServicingOperation>
    {
        public void Configure(EntityTypeBuilder<ServicingOperation> builder)
        {
            builder.ToTable("ServicingOperations");
            builder.HasKey(operation => operation.Id);
            builder.Property(operation => operation.Id).ValueGeneratedNever();

            builder.Property(operation => operation.RequestHash).HasMaxLength(128).IsRequired();
            builder.Property(operation => operation.QuoteRef).HasMaxLength(256);

            builder.HasIndex(operation => new { operation.OrderId, operation.Status });
            builder.HasIndex(operation => operation.CommandReceiptId);
        }
    }
}
