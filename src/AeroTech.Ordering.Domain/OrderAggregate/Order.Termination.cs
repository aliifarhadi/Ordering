using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
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

            var reversals = AppendServiceReversalLines(serviceIds, OrderPricingReason.Void, idGenerator);

            if (Status == OrderStatus.Ticketed && _orderServices.All(service => service.DocumentStatus != OrderServiceDocumentStatus.Issued))
                TransitionTo(OrderStatus.Cancelled);

            IncrementCommercialVersion();

            var reversedLineIds = reversals.Select(line => line.Id).ToHashSet();
            var reversedLines = BuildPricingLines()
                .Where(line => reversedLineIds.Contains(line.LineId))
                .ToList();

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

        private void CancelAllServices(IIdGenerator idGenerator)
        {
            var serviceIds = _orderServices.Select(service => service.Id).ToList();

            foreach (var service in _orderServices)
                service.MarkCancelled();

            RollUpCancelledItems(serviceIds);
            ReverseAllPricingLines(OrderPricingReason.Cancel, idGenerator);
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

        private void ReverseAllPricingLines(OrderPricingReason reason, IIdGenerator idGenerator)
        {
            var reversals = new List<OrderPricingLine>();

            foreach (var line in _pricingLines)
            {
                if (IsReversalLine(line))
                    continue;

                if (line.Allocations.Count == 0)
                {
                    if (IsLineReversed(line.Id))
                        continue;

                    reversals.Add(NewReversalLine(line, reason, line.Amount, line.CurrencyId, line.EquivalentAmount, line.EquivalentCurrencyId, line.ExchangeRate, idGenerator));
                    continue;
                }

                var pending = line.Allocations.Where(allocation => !IsAllocationReversed(allocation.Id)).ToList();
                if (pending.Count == 0)
                    continue;

                var reversalLine = NewReversalLine(
                    line,
                    reason,
                    pending.Sum(allocation => allocation.Amount),
                    line.CurrencyId,
                    pending.Sum(allocation => allocation.EquivalentAmount),
                    line.EquivalentCurrencyId,
                    line.ExchangeRate,
                    idGenerator);

                foreach (var allocation in pending)
                    reversalLine.AllocateTo(CopyAllocation(allocation, idGenerator));

                reversals.Add(reversalLine);
            }

            CommitReversals(reversals);
        }

        private bool IsLineReversed(long pricingLineId)
            => _pricingLines.Any(line => line.OriginalPricingLineId == pricingLineId);

        private bool IsAllocationReversed(long allocationId)
            => _pricingLines.Any(line => line.Allocations.Any(allocation => allocation.OriginalAllocationId == allocationId));

        private IReadOnlyList<OrderPricingLine> AppendServiceReversalLines(IReadOnlyCollection<long> serviceIds, OrderPricingReason reason, IIdGenerator idGenerator)
        {
            var reversals = new List<OrderPricingLine>();

            foreach (var line in _pricingLines)
            {
                if (IsReversalLine(line))
                    continue;

                foreach (var allocation in line.Allocations)
                {
                    if (allocation.OrderServiceId is not { } serviceId || !serviceIds.Contains(serviceId))
                        continue;

                    if (IsAllocationReversed(allocation.Id))
                        continue;

                    var reversalLine = NewReversalLine(line, reason, allocation.Amount, allocation.CurrencyId, allocation.EquivalentAmount, allocation.EquivalentCurrencyId, allocation.ExchangeRate, idGenerator);
                    reversalLine.AllocateTo(CopyAllocation(allocation, idGenerator));
                    reversals.Add(reversalLine);
                }
            }

            CommitReversals(reversals);

            return reversals;
        }

        private static bool IsReversalLine(OrderPricingLine line)
            => line.LineReason is OrderPricingReason.Void or OrderPricingReason.Cancel;

        private OrderPricingLine NewReversalLine(
            OrderPricingLine line,
            OrderPricingReason reason,
            decimal amount,
            int currencyId,
            decimal equivalentAmount,
            int equivalentCurrencyId,
            ExchangeRate? exchangeRate,
            IIdGenerator idGenerator)
        {
            var reversalDirection = line.LineDirection == OrderPricingLineDirection.Credit
                ? OrderPricingLineDirection.Debit
                : OrderPricingLineDirection.Credit;

            return new OrderPricingLine(new CreateOrderPricingLineArgs(
                idGenerator.NewId(),
                Id,
                reason,
                line.LineScope,
                line.LineCategory,
                line.LineSubCategory,
                reversalDirection,
                line.Code,
                line.Description,
                line.Reference,
                amount,
                currencyId,
                false,
                equivalentAmount,
                equivalentCurrencyId,
                exchangeRate?.Copy(),
                line.Refundability,
                line.Id));
        }

        private static CreateOrderPricingLineAllocationArgs CopyAllocation(OrderPricingLineAllocation allocation, IIdGenerator idGenerator)
            => new(
                idGenerator.NewId(),
                allocation.OrderItemId,
                allocation.OrderServiceId,
                allocation.TargetType,
                allocation.TargetId,
                allocation.Amount,
                allocation.CurrencyId,
                allocation.EquivalentAmount,
                allocation.EquivalentCurrencyId,
                allocation.ExchangeRate?.Copy(),
                allocation.Id);

        private void CommitReversals(List<OrderPricingLine> reversals)
        {
            foreach (var reversal in reversals)
                _pricingLines.Add(reversal);

            RecalculateTotal();
        }
    }
}
