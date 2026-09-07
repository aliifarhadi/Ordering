using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Constants;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate;
using AeroTech.Ordering.Domain.TrafficDocumentAggregate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.TrafficDocumentAggregate
{
    public sealed class TrafficDocumentConfiguration : IEntityTypeConfiguration<TrafficDocument>
    {
        public void Configure(EntityTypeBuilder<TrafficDocument> builder)
        {
            builder.ToTable("TrafficDocuments", "Document");
            builder.HasKey(document => document.Id);
            builder.Property(document => document.Id).ValueGeneratedNever();

            builder.HasDiscriminator<string>("DocumentType")
                .HasValue<TicketDocument>("Ticket")
                .HasValue<EmdDocument>("Emd");

            builder.Property(document => document.DocumentNumber).HasMaxLength(20);
            builder.Property(document => document.DocumentUniqueCode).HasMaxLength(64);
            builder.Property(document => document.IssueReference).HasMaxLength(200);
            builder.Property(document => document.VoidReasonDetail).HasMaxLength(TrafficDocumentRules.MaxVoidReasonDetailLength);

            builder.HasIndex(document => document.OrderId);
            builder.HasIndex(document => document.DocumentNumber).IsUnique();

            builder.HasMany(document => document.Coupons)
                .WithOne()
                .HasForeignKey(coupon => coupon.TrafficDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(document => document.Coupons).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
