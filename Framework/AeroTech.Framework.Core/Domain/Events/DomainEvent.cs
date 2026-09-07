namespace AeroTech.Framework.Core.Domain.Events
{
    public abstract record DomainEvent(string EventId, string AggregateId, DateTimeOffset TimeOfOccurrence) : IDomainEvent
    {
        public string EventId { get; init; } = EventId;

        public string AggregateId { get; init; } = AggregateId;

        public DateTimeOffset TimeOfOccurrence { get; init; } = TimeOfOccurrence;
    }
}
