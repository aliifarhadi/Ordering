using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderIssued;
using IntegrationPricingLine = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderIssuedPricingLine;

namespace AeroTech.Ordering.Application.OrderAggregate.EventHandlers
{
    public sealed class PublishOrderIssuedIntegrationEvent : INotificationHandler<DomainEventNotification<OrderIssued>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishOrderIssuedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<OrderIssued> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.OrderId,
                @event.AirlineOfficeId,
                @event.RecordLocator,
                @event.UniqueIdentifierId,
                @event.CommercialVersion,
                @event.EventOrdinal,
                @event.Status,
                @event.Type,
                @event.Channel,
                @event.CustomerId,
                @event.IssuedAt,
                @event.GrandTotal,
                @event.CurrencyId,
                @event.PricingLines
                    .Select(line => new IntegrationPricingLine(
                        line.LineId,
                        line.OriginalAmount,
                        line.OriginalCurrencyId,
                        line.SaleAmount,
                        line.RateOfExchange,
                        line.NumberOfDecimalPlaces,
                        line.RateOfExchangeId,
                        line.RoundingFactor,
                        LegacyPricingLineTranslation.Category(line.ComponentType),
                        LegacyPricingLineTranslation.Direction(line.Direction),
                        line.Code,
                        line.Description,
                        line.SourceLineRef,
                        line.TrafficDocumentId,
                        line.DocumentCouponId))
                    .ToList()), @event, cancellationToken);
        }
    }
}
