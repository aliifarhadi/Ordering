using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderCancelled;
using IntegrationPricingLine = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderCancelledPricingLine;

namespace AeroTech.Ordering.Application.OrderAggregate.EventHandlers
{
    public sealed class PublishOrderCancelledIntegrationEvent : INotificationHandler<DomainEventNotification<OrderCancelled>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishOrderCancelledIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<OrderCancelled> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.OrderId,
                @event.AirlineOfficeId,
                @event.RecordLocator,
                @event.UniqueIdentifierId,
                @event.CommercialVersion,
                @event.Status,
                @event.Type,
                @event.Channel,
                @event.CustomerId,
                @event.Reason,
                @event.CancelledBy,
                @event.CancelledAt,
                @event.WasTicketed,
                @event.GrandTotal,
                @event.CurrencyId,
                @event.PricingLines
                    .Select(line => new IntegrationPricingLine(
                        line.LineId,
                        line.OriginalLineId,
                        line.Amount,
                        line.CurrencyId,
                        line.EquivalentAmount,
                        line.RateOfExchange,
                        line.NumberOfDecimalPlaces,
                        line.RateOfExchangeId,
                        line.RoundingFactor,
                        line.Category,
                        line.Direction,
                        line.Code,
                        line.Description,
                        line.Reference,
                        line.TrafficDocumentId,
                        line.DocumentCouponId))
                    .ToList()), @event, cancellationToken);
        }
    }
}
