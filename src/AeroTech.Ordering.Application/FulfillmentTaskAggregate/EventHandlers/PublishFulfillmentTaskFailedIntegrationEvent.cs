using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.FulfillmentTaskAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.FulfillmentTaskFailed;

namespace AeroTech.Ordering.Application.FulfillmentTaskAggregate.EventHandlers
{
    public sealed class PublishFulfillmentTaskFailedIntegrationEvent : INotificationHandler<DomainEventNotification<FulfillmentTaskFailed>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishFulfillmentTaskFailedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<FulfillmentTaskFailed> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.TaskId,
                @event.OrderId,
                @event.TaskType,
                @event.Purpose,
                @event.Error), @event, cancellationToken);
        }
    }
}
