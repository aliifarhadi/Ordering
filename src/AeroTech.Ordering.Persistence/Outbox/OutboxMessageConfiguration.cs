using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Outbox
{
    public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.ToTable("OutboxMessages", "dbo");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Id).ValueGeneratedOnAdd();
            builder.Property(message => message.MessageType).HasMaxLength(500);
            builder.Property(message => message.Payload).HasColumnType("nvarchar(max)");
            builder.HasIndex(message => message.ProcessedOn);
        }
    }
}
