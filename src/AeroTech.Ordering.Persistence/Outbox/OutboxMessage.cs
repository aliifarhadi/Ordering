namespace AeroTech.Ordering.Persistence.Outbox
{
    public sealed class OutboxMessage
    {
        public long Id { get; set; }

        public string MessageType { get; set; } = null!;

        public string Payload { get; set; } = null!;

        public DateTimeOffset OccurredOn { get; set; }

        public DateTimeOffset? ProcessedOn { get; set; }
    }
}
