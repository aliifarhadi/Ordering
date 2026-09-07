using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderExpired;

namespace AeroTech.Ordering.Application.OrderAggregate.EventHandlers
{
    public sealed class PublishOrderExpiredIntegrationEvent : INotificationHandler<DomainEventNotification<OrderExpired>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishOrderExpiredIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<OrderExpired> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.OrderId,
                @event.Status), @event, cancellationToken);
        }
    }
}
