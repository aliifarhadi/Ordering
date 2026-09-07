using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderPaid;

namespace AeroTech.Ordering.Application.OrderAggregate.EventHandlers
{
    public sealed class PublishOrderPaidIntegrationEvent : INotificationHandler<DomainEventNotification<OrderPaid>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishOrderPaidIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<OrderPaid> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.OrderId,
                @event.Status,
                @event.PaymentId,
                @event.Amount,
                @event.CurrencyId,
                @event.FormOfPayment,
                @event.ProviderReference,
                @event.PaidAt,
                @event.CustomerId,
                @event.AirlineOfficeId), @event, cancellationToken);
        }
    }
}
