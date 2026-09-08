using AeroTech.Framework.Core.Domain.Events;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application.OrderAggregate.EventHandlers;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;

namespace AeroTech.Ordering.Persistence.Tests._Shared
{
    public sealed class OutboxDomainEventDispatcher : IDomainEventDispatcher
    {
        private readonly Func<IOutboxWriter> _outboxWriter;

        public OutboxDomainEventDispatcher(Func<IOutboxWriter> outboxWriter) => _outboxWriter = outboxWriter;

        public List<IDomainEvent> Dispatched { get; } = new();

        public async Task DispatchAsync(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            foreach (var domainEvent in domainEvents)
            {
                Dispatched.Add(domainEvent);

                if (domainEvent is OrderPricingChanged pricingChanged)
                    await new PublishOrderPricingChangedIntegrationEvent(_outboxWriter())
                        .Handle(new DomainEventNotification<OrderPricingChanged>(pricingChanged), cancellationToken);
            }
        }
    }
}
