using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderCreated;

namespace AeroTech.Ordering.Application.OrderAggregate.EventHandlers
{
    public sealed class PublishOrderCreatedIntegrationEvent : INotificationHandler<DomainEventNotification<OrderCreated>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishOrderCreatedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<OrderCreated> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.OrderId,
                @event.UniqueIdentifierId,
                @event.CustomerId,
                @event.AirlineOfficeId,
                @event.CreatorUserId,
                @event.Channel,
                @event.Status,
                @event.Type,
                @event.CurrencyId,
                @event.Pax,
                @event.GrandTotal,
                @event.TotalTax,
                @event.CommissionAmount,
                @event.CommissionRate,
                @event.CommercialVersion,
                @event.LinkedOrderId,
                @event.LinkedPNR,
                @event.TimeToLive,
                @event.CreationDate), @event, cancellationToken);
        }
    }
}
