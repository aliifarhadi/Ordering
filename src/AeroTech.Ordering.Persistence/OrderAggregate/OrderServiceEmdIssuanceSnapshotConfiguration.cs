using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.OrderAggregate
{
    public sealed class OrderServiceEmdIssuanceSnapshotConfiguration : IEntityTypeConfiguration<OrderServiceEmdIssuanceSnapshot>
    {
        public void Configure(EntityTypeBuilder<OrderServiceEmdIssuanceSnapshot> builder)
        {
            builder.ToTable("OrderServiceEmdIssuanceSnapshots");
            builder.HasKey(snapshot => snapshot.Id);
            builder.Property(snapshot => snapshot.Id).ValueGeneratedNever();
            builder.Property(snapshot => snapshot.ReasonForIssuanceCode).HasMaxLength(8).IsRequired();
            builder.Property(snapshot => snapshot.ReasonForIssuanceSubCode).HasMaxLength(8).IsRequired();
            builder.Property(snapshot => snapshot.DocumentGroupReference).HasMaxLength(128);
            builder.Property(snapshot => snapshot.SourceSystem).HasMaxLength(64);
            builder.Property(snapshot => snapshot.SourceReference).HasMaxLength(128);

            builder.HasIndex(snapshot => snapshot.OrderServiceId).IsUnique();
            builder.HasIndex(snapshot => snapshot.AssociatedAirOrderServiceId);

            builder.HasOne<OrderService>()
                .WithMany()
                .HasForeignKey(snapshot => snapshot.AssociatedAirOrderServiceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
