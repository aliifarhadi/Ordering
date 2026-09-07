using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.DocumentStockAggregate
{
    public sealed class DocumentStockConfiguration : IEntityTypeConfiguration<DocumentStock>
    {
        public void Configure(EntityTypeBuilder<DocumentStock> builder)
        {
            builder.ToTable("DocumentStocks");
            builder.HasKey(stock => stock.Id);
            builder.Property(stock => stock.Id).ValueGeneratedNever();
            builder.Property(stock => stock.DocumentType).HasMaxLength(16).IsRequired();
            builder.Property(stock => stock.Prefix).HasMaxLength(16).IsRequired();
            builder.Property(stock => stock.CheckDigitProfile).HasMaxLength(32).IsRequired();

            builder.HasIndex(stock => new { stock.OwnerAirlineId, stock.DocumentType, stock.Status });

            builder.HasMany(stock => stock.Allocations)
                .WithOne()
                .HasForeignKey(allocation => allocation.DocumentStockId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(stock => stock.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public sealed class DocumentStockAllocationConfiguration : IEntityTypeConfiguration<DocumentStockAllocation>
    {
        public void Configure(EntityTypeBuilder<DocumentStockAllocation> builder)
        {
            builder.ToTable("DocumentStockAllocations");
            builder.HasKey(allocation => allocation.Id);
            builder.Property(allocation => allocation.Id).ValueGeneratedNever();
            builder.Property(allocation => allocation.DocumentRole).HasMaxLength(64).IsRequired();
            builder.Property(allocation => allocation.DocumentNumber).HasMaxLength(32).IsRequired();

            builder.HasIndex(allocation => new { allocation.DocumentStockId, allocation.Serial }).IsUnique();
            builder.HasIndex(allocation => new { allocation.OperationId, allocation.DocumentRole }).IsUnique();
            builder.HasIndex(allocation => allocation.DocumentNumber).IsUnique();
        }
    }
}
