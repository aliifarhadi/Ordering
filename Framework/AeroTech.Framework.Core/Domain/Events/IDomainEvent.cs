namespace AeroTech.Framework.Core.Domain.Events
{
    public interface IDomainEvent : IEvent
    {
        string AggregateId { get; }
    }
}
