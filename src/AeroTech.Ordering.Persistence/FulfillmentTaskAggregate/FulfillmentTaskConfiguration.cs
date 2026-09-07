using AeroTech.Ordering.Domain.FulfillmentTaskAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.FulfillmentTaskAggregate
{
    public sealed class FulfillmentTaskConfiguration : IEntityTypeConfiguration<FulfillmentTask>
    {
        public void Configure(EntityTypeBuilder<FulfillmentTask> builder)
        {
            builder.ToTable("FulfillmentTasks", "Fulfillment");
            builder.HasKey(task => task.Id);
            builder.Property(task => task.Id).ValueGeneratedNever();

            builder.Property(task => task.IdempotencyKey).HasMaxLength(200);
            builder.Property(task => task.SupplierCode).HasMaxLength(50);
            builder.Property(task => task.LastError).HasColumnType("nvarchar(max)");

            builder.HasIndex(task => task.OrderId);
            builder.HasIndex(task => new { task.Status, task.NextRetryAt });

            builder.HasMany(task => task.Targets)
                .WithOne()
                .HasForeignKey(target => target.FulfillmentTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(task => task.Attempts)
                .WithOne()
                .HasForeignKey(attempt => attempt.FulfillmentTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(task => task.Targets).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(task => task.Attempts).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
