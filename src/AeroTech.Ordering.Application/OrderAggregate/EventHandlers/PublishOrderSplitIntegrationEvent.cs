using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using MediatR;
using IntegrationEvent = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderSplit;
using IntegrationNewOrder = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderSplitNewOrder;
using IntegrationPricingLine = AeroTech.Messages.Ordering.IntegrationEvents.V1.OrderSplitPricingLine;

namespace AeroTech.Ordering.Application.OrderAggregate.EventHandlers
{
    public sealed class PublishOrderSplitIntegrationEvent : INotificationHandler<DomainEventNotification<OrderSplit>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishOrderSplitIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<OrderSplit> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new IntegrationEvent(
                @event.OrderId,
                @event.AirlineOfficeId,
                @event.RecordLocator,
                @event.UniqueIdentifierId,
                @event.Version,
                @event.Status,
                @event.Type,
                @event.Channel,
                @event.CustomerId,
                @event.SplitAt,
                @event.MovedTravellerIds,
                @event.WasTicketed,
                @event.GrandTotal,
                @event.CurrencyId,
                Map(@event.PricingLines),
                new IntegrationNewOrder(
                    @event.NewOrder.OrderId,
                    @event.NewOrder.RecordLocator,
                    @event.NewOrder.UniqueIdentifierId,
                    @event.NewOrder.Version,
                    @event.NewOrder.GrandTotal,
                    Map(@event.NewOrder.PricingLines))), @event, cancellationToken);
        }

        private static IReadOnlyList<IntegrationPricingLine> Map(IReadOnlyList<PricingLineSnapshot> lines)
            => lines
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
                .ToList();
    }
}
