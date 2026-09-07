using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderReservationUnconfirmed;

namespace AeroTech.Ordering.Application.OrderAggregate.EventHandlers
{
    public sealed class PublishOrderReservationUnconfirmedIntegrationEvent : INotificationHandler<DomainEventNotification<OrderReservationUnconfirmed>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishOrderReservationUnconfirmedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<OrderReservationUnconfirmed> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.OrderId,
                @event.CustomerId,
                @event.AirlineOfficeId,
                @event.Status,
                @event.Reason,
                @event.Detail), @event, cancellationToken);
        }
    }
}
