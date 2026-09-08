using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Application._Shared.Events;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using MediatR;
using Contract = AeroTech.Messages.Ordering.IntegrationEvents.V1;

namespace AeroTech.Ordering.Application.OrderAggregate.EventHandlers
{
    public sealed class PublishOrderPricingChangedIntegrationEvent
        : INotificationHandler<DomainEventNotification<OrderPricingChanged>>
    {
        private readonly IOutboxWriter _outboxWriter;

        public PublishOrderPricingChangedIntegrationEvent(IOutboxWriter outboxWriter) => _outboxWriter = outboxWriter;

        public Task Handle(DomainEventNotification<OrderPricingChanged> notification, CancellationToken cancellationToken)
        {
            var @event = notification.DomainEvent;

            return _outboxWriter.WriteAsync(new Contract.OrderPricingChanged(
                @event.OrderId,
                @event.OwnerAirlineId,
                @event.OrderChangeId,
                @event.OperationId,
                @event.PriceChangeSetId,
                @event.FinancialSequence,
                @event.CommercialVersion,
                @event.EventOrdinal,
                @event.ObligationVersion,
                @event.Reason,
                @event.PricingSource,
                @event.SourceOfferId,
                @event.SourcePricingReference,
                @event.CommittedAt,
                @event.CurrencyId,
                @event.CustomerBalanceImpact,
                @event.CustomerTotalAfter,
                @event.PricingLines.Select(Describe).ToList()), @event, cancellationToken);
        }

        private static Contract.OrderPricingChangedLine Describe(PricingChangeLine line)
            => new(
                line.PricingLineId,
                line.ComponentType,
                line.Effect,
                line.Direction,
                line.LineRole,
                line.OriginalAmount,
                line.OriginalCurrencyId,
                line.SaleAmount,
                line.SaleCurrencyId,
                Describe(line.ExchangeRate),
                line.Refundability,
                line.BasisType,
                line.BasisReferenceId,
                line.OrderItemId,
                line.ApplicationLevel,
                line.Quantity,
                line.UnitOfMeasure,
                line.UnitPrice,
                line.Code,
                line.Description,
                line.SourceLineRef,
                line.OccurrenceKey,
                line.OriginalPricingLineId,
                line.OriginalAllocationId,
                line.RelatedOperationId,
                line.SettlementPartyRef,
                line.SettlementCategory,
                line.AllocationSets.Select(Describe).ToList());

        private static Contract.OrderPricingChangedAllocationSet Describe(PricingChangeAllocationSet set)
            => new(
                set.AllocationSetId,
                set.Purpose,
                set.Version,
                set.Source,
                set.Method,
                set.Completeness,
                set.SupersedesAllocationSetId,
                set.PricingContextRef,
                set.PolicyVersion,
                set.Allocations.Select(Describe).ToList());

        private static Contract.OrderPricingChangedAllocation Describe(PricingChangeAllocation allocation)
            => new(
                allocation.AllocationId,
                allocation.SaleAmount,
                allocation.SaleCurrencyId,
                allocation.OrderItemId,
                allocation.OrderServiceId,
                allocation.TravellerId,
                allocation.ItineraryId,
                allocation.SegmentId,
                allocation.CoveragePortionRef,
                allocation.OriginalAmount,
                allocation.OriginalCurrencyId,
                Describe(allocation.ExchangeRate),
                allocation.OriginalAllocationId);

        private static Contract.OrderPricingChangedExchangeRate? Describe(PricingChangeExchangeRate? rate)
            => rate is null
                ? null
                : new Contract.OrderPricingChangedExchangeRate(
                    rate.RateOfExchange,
                    rate.NumberOfDecimalPlaces,
                    rate.RateOfExchangeId,
                    rate.RoundingFactor);
    }
}
