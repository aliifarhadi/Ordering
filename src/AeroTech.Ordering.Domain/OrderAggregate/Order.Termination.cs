using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public void MarkDocumentVoided(
            long documentId,
            string documentNumber,
            IReadOnlyCollection<long> serviceIds,
            VoidReason reason,
            long voidedBy,
            DateTimeOffset voidedAt,
            IIdGenerator idGenerator)
        {
            foreach (var service in _orderServices)
                if (serviceIds.Contains(service.Id))
                    service.MarkVoided();

            RollUpCancelledItems(serviceIds);

            var changeSet = ReverseServiceValue(serviceIds, OrderChangeType.Cancel, PriceChangeReason.Void, voidedAt, idGenerator);

            if (Status == OrderStatus.Ticketed && _orderServices.All(service => service.DocumentStatus != OrderServiceDocumentStatus.Issued))
                TransitionTo(OrderStatus.Cancelled);

            IncrementCommercialVersion();

            var reversedLines = changeSet is null ? [] : BuildPricingLines(changeSet.Id);

            Causes(new OrderDocumentVoided(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                voidedAt,
                documentId,
                documentNumber,
                Id,
                AirlineOfficeId,
                RecordLocator?.Value,
                UniqueIdentifierId,
                CommercialVersion,
                NextEventOrdinal(),
                Status,
                Type,
                Channel,
                Amount.GrandTotal,
                CurrencyId,
                CustomerId,
                reason,
                voidedBy,
                voidedAt,
                NetOf(reversedLines),
                reversedLines));
        }

        private OrderPriceChangeSet? CancelAllServices(
            DateTimeOffset cancelledAt,
            long cancelledBy,
            long? operationId,
            IIdGenerator idGenerator)
        {
            var serviceIds = _orderServices.Select(service => service.Id).ToList();

            foreach (var service in _orderServices)
                service.MarkCancelled();

            RollUpCancelledItems(serviceIds);

            return ReverseOutstandingValue(
                OrderChangeType.Cancel,
                PriceChangeReason.Cancellation,
                PricingSource.OrderingDerived,
                cancelledBy,
                operationId,
                cancelledAt,
                idGenerator);
        }

        private void RollUpCancelledItems(IReadOnlyCollection<long> serviceIds)
        {
            var affectedItemIds = _orderServices
                .Where(service => serviceIds.Contains(service.Id))
                .Select(service => service.OrderItemId)
                .Distinct()
                .ToList();

            foreach (var itemId in affectedItemIds)
            {
                var allCancelled = _orderServices
                    .Where(service => service.OrderItemId == itemId)
                    .All(service => service.Status == OrderServiceStatus.Cancelled);

                if (allCancelled)
                    _items.First(item => item.Id == itemId).MarkCancelled();
            }
        }

        private OrderPriceChangeSet? ReverseOutstandingValue(
            OrderChangeType changeType,
            PriceChangeReason reason,
            PricingSource source,
            long? actorId,
            long? operationId,
            DateTimeOffset occurredAt,
            IIdGenerator idGenerator)
        {
            var reversals = new List<AcceptedPricingLineArgs>();

            foreach (var line in ReversibleLines())
            {
                var outstandingSale = OutstandingSaleOf(line);

                if (outstandingSale <= 0m)
                    continue;

                reversals.Add(ReversalOf(line, outstandingSale, OutstandingOriginalOf(line), null));
            }

            return CommitReversals(reversals, changeType, reason, source, actorId, operationId, occurredAt, idGenerator);
        }

        private OrderPriceChangeSet? ReverseServiceValue(
            IReadOnlyCollection<long> serviceIds,
            OrderChangeType changeType,
            PriceChangeReason reason,
            DateTimeOffset occurredAt,
            IIdGenerator idGenerator)
        {
            var reversals = new List<AcceptedPricingLineArgs>();

            foreach (var line in ReversibleLines())
            {
                var outstandingSale = OutstandingSaleOf(line);

                if (outstandingSale <= 0m)
                    continue;

                var allocations = line.CommercialAllocations()
                    .Where(allocation => allocation.OrderServiceId is { } serviceId && serviceIds.Contains(serviceId))
                    .ToList();

                decimal saleAmount;
                decimal originalAmount;
                long? originalAllocationId = null;

                if (allocations.Count > 0)
                {
                    saleAmount = allocations.Sum(allocation => allocation.SaleAmount);
                    originalAmount = allocations.All(allocation => allocation.OriginalAmount.HasValue)
                        ? allocations.Sum(allocation => allocation.OriginalAmount!.Value)
                        : DefensibleOriginalAmount(line, saleAmount);
                    originalAllocationId = allocations.Count == 1 ? allocations[0].Id : null;
                }
                else if (line.BasisType == PricingBasisType.OrderService
                         && line.BasisReferenceId is { } basisServiceId
                         && serviceIds.Contains(basisServiceId))
                {
                    saleAmount = outstandingSale;
                    originalAmount = OutstandingOriginalOf(line);
                }
                else
                {
                    continue;
                }

                saleAmount = Math.Min(saleAmount, outstandingSale);
                originalAmount = Math.Min(originalAmount, OutstandingOriginalOf(line));

                if (saleAmount <= 0m)
                    continue;

                reversals.Add(ReversalOf(line, saleAmount, originalAmount, originalAllocationId));
            }

            return CommitReversals(reversals, changeType, reason, PricingSource.PricingEngine, null, null, occurredAt, idGenerator);
        }

        private static decimal DefensibleOriginalAmount(OrderPricingLine line, decimal saleAmount)
            => line.ExchangeRate is null && line.OriginalCurrencyId == line.SaleCurrencyId
                ? saleAmount
                : 0m;

        private IEnumerable<OrderPricingLine> ReversibleLines()
            => _pricingLines.Where(line => line.LineRole == PricingLineRole.Original).ToList();

        private decimal OutstandingSaleOf(OrderPricingLine line) => line.SaleAmount - ReversedSaleAmount(line.Id);

        private decimal OutstandingOriginalOf(OrderPricingLine line) => line.OriginalAmount - ReversedOriginalAmount(line.Id, []);

        private static AcceptedPricingLineArgs ReversalOf(
            OrderPricingLine line,
            decimal saleAmount,
            decimal originalAmount,
            long? originalAllocationId)
            => new(
                line.ComponentType,
                line.Effect,
                PricingComponentPolicy.Opposite(line.Direction),
                PricingLineRole.Reversal,
                originalAmount,
                line.OriginalCurrencyId,
                saleAmount,
                line.SaleCurrencyId,
                line.BasisType,
                line.Refundability,
                OrderItemId: line.OrderItemId,
                Code: line.Code,
                Description: line.Description,
                ExchangeRate: line.ExchangeRate?.Copy(),
                ApplicationLevel: line.ApplicationLevel,
                BasisReferenceId: line.BasisReferenceId,
                OriginalPricingLineId: line.Id,
                OriginalAllocationId: originalAllocationId,
                SettlementPartyRef: line.SettlementPartyRef,
                SettlementCategory: line.SettlementCategory);

        private OrderPriceChangeSet? CommitReversals(
            IReadOnlyList<AcceptedPricingLineArgs> reversals,
            OrderChangeType changeType,
            PriceChangeReason reason,
            PricingSource source,
            long? actorId,
            long? operationId,
            DateTimeOffset occurredAt,
            IIdGenerator idGenerator)
            => reversals.Count == 0
                ? null
                : CommitPriceChange(
                    new AcceptedPriceChangeArgs(
                        changeType,
                        reason,
                        source,
                        reversals,
                        ActorId: actorId,
                        OperationId: operationId),
                    idGenerator,
                    occurredAt);
    }
}
