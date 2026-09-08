using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderDocumentVoided;
using IntegrationPricingLine = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderDocumentVoidedPricingLine;

namespace AeroTech.Ordering.Application.OrderAggregate.EventHandlers
{
    public sealed class PublishOrderDocumentVoidedIntegrationEvent : INotificationHandler<DomainEventNotification<OrderDocumentVoided>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishOrderDocumentVoidedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<OrderDocumentVoided> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.DocumentId,
                @event.DocumentNumber,
                @event.OrderId,
                @event.AirlineOfficeId,
                @event.RecordLocator,
                @event.UniqueIdentifierId,
                @event.CommercialVersion,
                @event.EventOrdinal,
                @event.Status,
                @event.Type,
                @event.Channel,
                @event.GrandTotal,
                @event.CurrencyId,
                @event.CustomerId,
                @event.Reason,
                @event.VoidedBy,
                @event.VoidedAt,
                @event.ReversedAmount,
                @event.PricingLines
                    .Select(line => new IntegrationPricingLine(
                        line.LineId,
                        line.OriginalLineId,
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
                        line.DocumentCouponId))
                    .ToList()), @event, cancellationToken);
        }
    }
}
