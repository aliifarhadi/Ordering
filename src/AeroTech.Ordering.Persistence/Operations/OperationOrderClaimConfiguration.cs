using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Operations
{
    public sealed class OperationOrderClaimConfiguration : IEntityTypeConfiguration<OperationOrderClaim>
    {
        public void Configure(EntityTypeBuilder<OperationOrderClaim> builder)
        {
            builder.ToTable("OperationOrderClaims");
            builder.HasKey(claim => claim.Id);
            builder.Property(claim => claim.Id).ValueGeneratedNever();

            builder
                .HasIndex(claim => claim.OrderId)
                .IsUnique()
                .HasFilter("[IsBlocking] = 1");

            builder.HasIndex(claim => claim.OperationId);
        }
    }
}
