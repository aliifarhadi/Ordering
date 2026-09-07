using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.FulfillmentTaskSucceeded;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.EventHandlers
{
    public sealed class PublishFulfillmentTaskSucceededIntegrationEvent : INotificationHandler<DomainEventNotification<FulfillmentTaskSucceeded>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishFulfillmentTaskSucceededIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<FulfillmentTaskSucceeded> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.TaskId,
                @event.OrderId,
                @event.TaskType,
                @event.Purpose), @event, cancellationToken);
        }
    }
}
