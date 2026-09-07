using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AeroTech.Ordering.Persistence.Inbox
{
    public sealed class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
    {
        public void Configure(EntityTypeBuilder<InboxMessage> builder)
        {
            builder.ToTable("InboxMessages", "dbo");
            builder.HasKey(message => new { message.MessageId, message.Consumer });
            builder.Property(message => message.Consumer).HasMaxLength(256);
            builder.Property(message => message.MessageType).HasMaxLength(500);
            builder.HasIndex(message => message.ReceivedOn);
        }
    }
}
