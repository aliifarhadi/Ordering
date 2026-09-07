namespace AeroTech.Ordering.Persistence.Inbox
{
    public sealed class InboxMessage
    {
        public Guid MessageId { get; set; }

        public string Consumer { get; set; } = null!;

        public string MessageType { get; set; } = null!;

        public DateTimeOffset ReceivedOn { get; set; }
    }
}
