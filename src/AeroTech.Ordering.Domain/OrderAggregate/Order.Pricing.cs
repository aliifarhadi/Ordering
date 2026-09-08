using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public decimal CustomerTotal => _pricingLines
            .Where(line => line.AffectsCustomerBalance)
            .Sum(line => line.SignedSaleAmount);

        public OrderPriceChangeSet CommitPriceChange(AcceptedPriceChangeArgs change, IIdGenerator idGenerator, IClock clock)
            => CommitPriceChange(change, idGenerator, clock.GetDateTime());

        public OrderPriceChangeSet CommitPriceChange(AcceptedPriceChangeArgs change, IIdGenerator idGenerator, DateTimeOffset now)
        {
            if (change.Lines.Count == 0)
                throw ExceptionFactory.PriceChangeSetRequiresLines();

            var totalBefore = CustomerTotal;

            var orderChange = new OrderChange(new CreateOrderChangeArgs(
                idGenerator.NewId(),
                Id,
                change.ChangeType,
                change.Source,
                now,
                change.ChangeReason,
                change.ExternalReference,
                change.ActorScope,
                change.ActorId,
                change.OperationId));

            _changes.Add(orderChange);

            FinancialSequence++;

            var changeSet = new OrderPriceChangeSet(new CreateOrderPriceChangeSetArgs(
                idGenerator.NewId(),
                Id,
                orderChange.Id,
                FinancialSequence,
                CommercialVersion,
                change.Reason,
                change.Source,
                now,
                change.SourceOfferId,
                change.SourcePricingRef));

            _priceChangeSets.Add(changeSet);

            foreach (var accepted in change.Lines)
                AppendPricingLine(changeSet, accepted, now, idGenerator);

            changeSet.Commit(now);

            RecomputeAmountCache();

            if (CustomerTotal != totalBefore)
                AdvanceObligationVersion();

            return changeSet;
        }

        private void AppendPricingLine(
            OrderPriceChangeSet changeSet,
            AcceptedPricingLineArgs accepted,
            DateTimeOffset now,
            IIdGenerator idGenerator)
        {
            EnsureSourceLineNotAlreadyAccepted(accepted.SourceLineRef);
            EnsureSaleCurrencyIsCoherent(accepted);
            EnsureReversalIsBounded(accepted);

            var line = new OrderPricingLine(new CreateOrderPricingLineArgs(
                idGenerator.NewId(),
                Id,
                changeSet.Id,
                accepted.ComponentType,
                accepted.Effect,
                accepted.Direction,
                accepted.LineRole,
                accepted.OriginalAmount,
                accepted.OriginalCurrencyId,
                accepted.SaleAmount,
                accepted.SaleCurrencyId,
                accepted.BasisType,
                accepted.Refundability,
                now,
                accepted.OrderItemId,
                accepted.Code,
                accepted.Description,
                accepted.ExchangeRate,
                accepted.ApplicationLevel,
                accepted.Quantity,
                accepted.UnitOfMeasure,
                accepted.UnitPrice,
                accepted.BasisReferenceId,
                accepted.SourceLineRef,
                accepted.OriginalPricingLineId,
                accepted.OriginalAllocationId,
                accepted.TransferGroupId,
                accepted.RelatedOperationId,
                accepted.CalculationSnapshot,
                accepted.TaxDetails,
                accepted.SettlementPartyRef,
                accepted.SettlementCategory));

            foreach (var acceptedSet in accepted.AllocationSets ?? [])
                AppendAllocationSet(line, acceptedSet, now, idGenerator);

            _pricingLines.Add(line);
        }

        private void AppendAllocationSet(
            OrderPricingLine line,
            AcceptedPricingAllocationSetArgs acceptedSet,
            DateTimeOffset now,
            IIdGenerator idGenerator)
        {
            var version = line.AllocationSets.Count(set => set.Purpose == acceptedSet.Purpose) + 1;
            var superseded = line.ActiveAllocationSet(acceptedSet.Purpose);

            var set = line.AddAllocationSet(new CreateOrderPricingAllocationSetArgs(
                idGenerator.NewId(),
                Id,
                acceptedSet.Purpose,
                version,
                acceptedSet.Source,
                acceptedSet.Method,
                acceptedSet.Completeness,
                now,
                superseded?.Id,
                acceptedSet.PricingContextRef,
                acceptedSet.PolicyVersion));

            foreach (var allocation in acceptedSet.Allocations)
                set.Allocate(new CreateOrderPricingAllocationArgs(
                    idGenerator.NewId(),
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
                    allocation.ExchangeRate,
                    allocation.OriginalAllocationId));

            set.EnsureReconciles(line.SaleAmount, line.SaleCurrencyId);
        }

        private void EnsureSourceLineNotAlreadyAccepted(string? sourceLineRef)
        {
            if (string.IsNullOrWhiteSpace(sourceLineRef))
                return;

            if (_pricingLines.Any(line => line.SourceLineRef == sourceLineRef))
                throw ExceptionFactory.DuplicateSourceLineReference(sourceLineRef);
        }

        private void EnsureSaleCurrencyIsCoherent(AcceptedPricingLineArgs accepted)
        {
            if (accepted.Effect == PricingEffect.CustomerBalance && accepted.SaleCurrencyId != CurrencyId)
                throw ExceptionFactory.CustomerBalanceCurrencyMismatch(accepted.SaleCurrencyId, CurrencyId);
        }

        private void EnsureReversalIsBounded(AcceptedPricingLineArgs accepted)
        {
            if (accepted.LineRole != PricingLineRole.Reversal)
                return;

            if (accepted.OriginalPricingLineId is not { } originalId)
                throw ExceptionFactory.ReversalRequiresOriginalLine();

            var original = _pricingLines.FirstOrDefault(line => line.Id == originalId)
                ?? throw ExceptionFactory.OriginalPricingLineNotFound(originalId);

            if (original.ComponentType != accepted.ComponentType
                || original.Effect != accepted.Effect
                || original.SaleCurrencyId != accepted.SaleCurrencyId
                || original.OriginalCurrencyId != accepted.OriginalCurrencyId
                || accepted.Direction != PricingComponentPolicy.Opposite(original.Direction))
                throw ExceptionFactory.ReversalMustOpposeOriginal(originalId);

            var outstandingSale = original.SaleAmount - ReversedSaleAmount(originalId);

            if (accepted.SaleAmount > outstandingSale)
                throw ExceptionFactory.ReversalExceedsOutstandingValue(accepted.SaleAmount, originalId, outstandingSale);

            var outstandingOriginal = original.OriginalAmount - ReversedOriginalAmount(originalId);

            if (accepted.OriginalAmount > outstandingOriginal)
                throw ExceptionFactory.ReversalExceedsOutstandingValue(accepted.OriginalAmount, originalId, outstandingOriginal);
        }

        public decimal ReversedSaleAmount(long originalPricingLineId)
            => _pricingLines
                .Where(line => line.LineRole == PricingLineRole.Reversal && line.OriginalPricingLineId == originalPricingLineId)
                .Sum(line => line.SaleAmount);

        private decimal ReversedOriginalAmount(long originalPricingLineId)
            => _pricingLines
                .Where(line => line.LineRole == PricingLineRole.Reversal && line.OriginalPricingLineId == originalPricingLineId)
                .Sum(line => line.OriginalAmount);

        public decimal OutstandingSaleAmount(long pricingLineId)
        {
            var line = _pricingLines.FirstOrDefault(candidate => candidate.Id == pricingLineId)
                ?? throw ExceptionFactory.OriginalPricingLineNotFound(pricingLineId);

            return line.SaleAmount - ReversedSaleAmount(pricingLineId);
        }

        public IReadOnlyList<ServiceValueAttribution> ServiceValueAttributions(long orderServiceId)
        {
            var attributions = new List<ServiceValueAttribution>();

            foreach (var line in _pricingLines.Where(line => line.AffectsCustomerBalance))
            {
                var allocations = line.CommercialAllocations()
                    .Where(allocation => allocation.OrderServiceId == orderServiceId)
                    .ToList();

                if (allocations.Count > 0)
                {
                    foreach (var allocation in allocations)
                        attributions.Add(new ServiceValueAttribution(
                            line.Id,
                            line.ComponentType,
                            line.Effect,
                            PricingComponentPolicy.Sign(line.Direction) * allocation.SaleAmount,
                            allocation.SaleCurrencyId,
                            allocation.Id));

                    continue;
                }

                if (line.BasisType == PricingBasisType.OrderService && line.BasisReferenceId == orderServiceId)
                    attributions.Add(new ServiceValueAttribution(
                        line.Id,
                        line.ComponentType,
                        line.Effect,
                        line.SignedSaleAmount,
                        line.SaleCurrencyId,
                        null));
            }

            return attributions;
        }

        internal void RecomputeAmountCache()
        {
            decimal CustomerTotalOf(params PricingComponentType[] components) => _pricingLines
                .Where(line => line.AffectsCustomerBalance && components.Contains(line.ComponentType))
                .Sum(line => line.SignedSaleAmount);

            SetAmount(new OrderAmount(
                CustomerTotalOf(PricingComponentType.Fare),
                CustomerTotalOf(PricingComponentType.Tax),
                CustomerTotalOf(PricingComponentType.Fee),
                CustomerTotalOf(PricingComponentType.CarrierSurcharge),
                CustomerTotalOf(PricingComponentType.Discount),
                CustomerTotalOf(PricingComponentType.Penalty),
                CustomerTotalOf(PricingComponentType.ProductCharge),
                CustomerTotal));

            SetCommission(new Commission(
                Commission?.CommissionRate ?? 0m,
                _pricingLines
                    .Where(line => line.ComponentType == PricingComponentType.Commission)
                    .Sum(line => line.SignedSaleAmount)));
        }

        private IReadOnlyList<PricingLineSnapshot> BuildPricingLines(long? priceChangeSetId = null)
        {
            var lines = new List<PricingLineSnapshot>();

            foreach (var line in _pricingLines)
            {
                if (priceChangeSetId is not null && line.PriceChangeSetId != priceChangeSetId)
                    continue;

                var (trafficDocumentId, documentCouponId) = ResolveDocumentLink(line);

                lines.Add(new PricingLineSnapshot(
                    line.Id,
                    line.PriceChangeSetId,
                    line.OriginalPricingLineId,
                    line.OriginalAmount,
                    line.OriginalCurrencyId,
                    line.SaleAmount,
                    line.SaleCurrencyId,
                    line.ExchangeRate?.RateOfExchange,
                    line.ExchangeRate?.NumberOfDecimalPlaces,
                    line.ExchangeRate?.RateOfExchangeId,
                    line.ExchangeRate?.RoundingFactor,
                    line.ComponentType,
                    line.Effect,
                    line.Direction,
                    line.LineRole,
                    line.Code ?? string.Empty,
                    line.Description,
                    line.SourceLineRef,
                    trafficDocumentId,
                    documentCouponId));
            }

            return lines;
        }

        private static decimal NetOf(IEnumerable<PricingLineSnapshot> lines) =>
            lines.Where(line => line.Effect == PricingEffect.CustomerBalance)
                .Sum(line => PricingComponentPolicy.Sign(line.Direction) * line.SaleAmount);

        private (long? TrafficDocumentId, long? DocumentCouponId) ResolveDocumentLink(OrderPricingLine line)
        {
            var serviceIds = line.CommercialAllocations()
                .Where(allocation => allocation.OrderServiceId.HasValue)
                .Select(allocation => allocation.OrderServiceId!.Value)
                .Distinct()
                .ToList();

            if (serviceIds.Count != 1 && line.BasisType == PricingBasisType.OrderService && line.BasisReferenceId is { } basisServiceId)
                serviceIds = [basisServiceId];

            if (serviceIds.Count != 1)
                return (null, null);

            var service = _orderServices.FirstOrDefault(orderService => orderService.Id == serviceIds[0]);

            return (service?.TrafficDocumentId, service?.DocumentCouponId);
        }
    }
}
