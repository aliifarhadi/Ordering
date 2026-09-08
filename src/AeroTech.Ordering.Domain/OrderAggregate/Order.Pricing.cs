using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.DomainEvents;
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
            => AttachPriceChange(StagePriceChange(change, idGenerator, now), now);

        private StagedPriceChange StagePriceChange(
            AcceptedPriceChangeArgs change,
            IIdGenerator idGenerator,
            DateTimeOffset now)
            => StagePriceChange(StageOrderChange(change, idGenerator, now), change, idGenerator, now);

        private OrderChange StageOrderChange(
            AcceptedPriceChangeArgs change,
            IIdGenerator idGenerator,
            DateTimeOffset now)
            => new(new CreateOrderChangeArgs(
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

        private StagedPriceChange StagePriceChange(
            OrderChange orderChange,
            AcceptedPriceChangeArgs change,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            if (change.Lines.Count == 0)
                throw ExceptionFactory.PriceChangeSetRequiresLines();

            var changeSet = new OrderPriceChangeSet(new CreateOrderPriceChangeSetArgs(
                idGenerator.NewId(),
                Id,
                orderChange.Id,
                FinancialSequence + 1,
                CommercialVersion,
                change.Reason,
                change.Source,
                now,
                change.SourceOfferId,
                change.SourcePricingRef));

            var staged = new List<OrderPricingLine>();

            foreach (var accepted in change.Lines)
                staged.Add(StagePricingLine(changeSet, accepted, staged, now, idGenerator));

            return new StagedPriceChange(orderChange, changeSet, staged);
        }

        private OrderPriceChangeSet AttachPriceChange(StagedPriceChange staged, DateTimeOffset now)
        {
            var totalBefore = CustomerTotal;

            _changes.Add(staged.Change);
            _priceChangeSets.Add(staged.ChangeSet);
            _pricingLines.AddRange(staged.Lines);

            FinancialSequence = staged.ChangeSet.FinancialSequence;

            staged.ChangeSet.Commit(now);

            RecomputeAmountCache();

            if (CustomerTotal != totalBefore)
                AdvanceObligationVersion();

            return staged.ChangeSet;
        }

        internal void RaisePricingChanged(
            OrderChange change,
            OrderPriceChangeSet changeSet,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            var lines = _pricingLines.Where(line => line.PriceChangeSetId == changeSet.Id).ToList();

            Causes(new OrderPricingChanged(
                idGenerator.NewId().ToString(),
                Id.ToString(),
                now,
                Id,
                OwnerAirlineId,
                change.Id,
                change.OperationId,
                changeSet.Id,
                changeSet.FinancialSequence,
                CommercialVersion,
                NextEventOrdinal(),
                ObligationVersion,
                changeSet.Reason,
                changeSet.Source,
                changeSet.SourceOfferId,
                changeSet.SourcePricingRef,
                changeSet.CommittedAt ?? now,
                CurrencyId,
                lines.Where(line => line.AffectsCustomerBalance).Sum(line => line.SignedSaleAmount),
                CustomerTotal,
                lines.Select(DescribePricingLine).ToList()));
        }

        private static PricingChangeLine DescribePricingLine(OrderPricingLine line)
            => new(
                line.Id,
                line.ComponentType,
                line.Effect,
                line.Direction,
                line.LineRole,
                line.OriginalAmount,
                line.OriginalCurrencyId,
                line.SaleAmount,
                line.SaleCurrencyId,
                DescribeExchangeRate(line.ExchangeRate),
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
                line.AllocationSets.Select(DescribeAllocationSet).ToList());

        private static PricingChangeAllocationSet DescribeAllocationSet(OrderPricingAllocationSet set)
            => new(
                set.Id,
                set.Purpose,
                set.Version,
                set.Source,
                set.Method,
                set.Completeness,
                set.SupersedesAllocationSetId,
                set.PricingContextRef,
                set.PolicyVersion,
                set.Allocations.Select(DescribeAllocation).ToList());

        private static PricingChangeAllocation DescribeAllocation(OrderPricingAllocation allocation)
            => new(
                allocation.Id,
                allocation.SaleAmount,
                allocation.SaleCurrencyId,
                allocation.OrderItemIdAtAllocation,
                allocation.OrderServiceId,
                allocation.TravellerId,
                allocation.ItineraryIdAtAllocation,
                allocation.SegmentIdAtAllocation,
                allocation.CoveragePortionRef,
                allocation.OriginalAmount,
                allocation.OriginalCurrencyId,
                DescribeExchangeRate(allocation.ExchangeRate),
                allocation.OriginalAllocationId);

        private static PricingChangeExchangeRate? DescribeExchangeRate(ValueObjects.ExchangeRate? rate)
            => rate is null
                ? null
                : new PricingChangeExchangeRate(
                    rate.RateOfExchange,
                    rate.NumberOfDecimalPlaces,
                    rate.RateOfExchangeId,
                    rate.RoundingFactor);

        private OrderPricingLine StagePricingLine(
            OrderPriceChangeSet changeSet,
            AcceptedPricingLineArgs accepted,
            IReadOnlyList<OrderPricingLine> staged,
            DateTimeOffset now,
            IIdGenerator idGenerator)
        {
            EnsureSourceOccurrenceIsUnique(accepted, staged);
            EnsureSaleCurrencyIsCoherent(accepted);
            EnsureReversalIsWellFormed(accepted, staged);

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
                accepted.OccurrenceKey,
                accepted.OriginalPricingLineId,
                accepted.OriginalAllocationId,
                accepted.TransferGroupId,
                accepted.RelatedOperationId,
                accepted.CalculationSnapshot,
                accepted.TaxDetails,
                accepted.SettlementPartyRef,
                accepted.SettlementCategory));

            foreach (var acceptedSet in accepted.AllocationSets ?? [])
                StageAllocationSet(line, acceptedSet, now, idGenerator);

            return line;
        }

        private void StageAllocationSet(
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

            set.EnsureReconciles(line.SaleAmount, line.SaleCurrencyId, line.OriginalAmount, line.OriginalCurrencyId);
        }

        private static void EnsureSourceOccurrenceIsUnique(
            AcceptedPricingLineArgs accepted,
            IReadOnlyList<OrderPricingLine> staged)
        {
            if (string.IsNullOrWhiteSpace(accepted.SourceLineRef))
                return;

            if (staged.Any(line => line.SourceLineRef == accepted.SourceLineRef
                                   && line.OccurrenceKey == accepted.OccurrenceKey))
                throw ExceptionFactory.DuplicateSourceOccurrence(accepted.SourceLineRef, accepted.OccurrenceKey ?? "-");
        }

        private void EnsureSaleCurrencyIsCoherent(AcceptedPricingLineArgs accepted)
        {
            if (accepted.Effect == PricingEffect.CustomerBalance && accepted.SaleCurrencyId != CurrencyId)
                throw ExceptionFactory.CustomerBalanceCurrencyMismatch(accepted.SaleCurrencyId, CurrencyId);
        }

        private void EnsureReversalIsWellFormed(
            AcceptedPricingLineArgs accepted,
            IReadOnlyList<OrderPricingLine> staged)
        {
            if (accepted.LineRole != PricingLineRole.Reversal)
                return;

            if (accepted.OriginalPricingLineId is not { } originalId)
                throw ExceptionFactory.ReversalRequiresOriginalLine();

            var original = _pricingLines.FirstOrDefault(line => line.Id == originalId)
                ?? throw ExceptionFactory.OriginalPricingLineNotFound(originalId);

            if (original.LineRole == PricingLineRole.Reversal)
                throw ExceptionFactory.ReversalCannotReverseAReversal(originalId);

            if (original.ComponentType != accepted.ComponentType
                || original.Effect != accepted.Effect
                || original.SaleCurrencyId != accepted.SaleCurrencyId
                || original.OriginalCurrencyId != accepted.OriginalCurrencyId
                || accepted.Direction != PricingComponentPolicy.Opposite(original.Direction))
                throw ExceptionFactory.ReversalMustOpposeOriginal(originalId);

            if (original.ExchangeRate is not null && !original.ExchangeRate.Equals(accepted.ExchangeRate))
                throw ExceptionFactory.ReversalMustPreserveConversionProvenance(originalId);

            var outstandingSale = original.SaleAmount - ReversedSaleAmount(originalId, staged);

            if (accepted.SaleAmount > outstandingSale)
                throw ExceptionFactory.ReversalExceedsOutstandingValue(accepted.SaleAmount, originalId, outstandingSale);

            var outstandingOriginal = original.OriginalAmount - ReversedOriginalAmount(originalId, staged);

            if (accepted.OriginalAmount > outstandingOriginal)
                throw ExceptionFactory.ReversalExceedsOutstandingValue(accepted.OriginalAmount, originalId, outstandingOriginal);

            if (outstandingOriginal > 0m && accepted.OriginalAmount <= 0m)
                throw ExceptionFactory.ReversalRequiresOriginalCurrencyAmount(originalId, outstandingOriginal);

            if (accepted.SaleAmount == outstandingSale && accepted.OriginalAmount != outstandingOriginal)
                throw ExceptionFactory.FullReversalMustMatchOutstandingOriginal(
                    originalId,
                    outstandingOriginal,
                    accepted.OriginalAmount);
        }

        public decimal ReversedSaleAmount(long originalPricingLineId)
            => ReversedSaleAmount(originalPricingLineId, []);

        private decimal ReversedSaleAmount(long originalPricingLineId, IReadOnlyList<OrderPricingLine> staged)
            => ReversalsOf(originalPricingLineId, staged).Sum(line => line.SaleAmount);

        private decimal ReversedOriginalAmount(long originalPricingLineId, IReadOnlyList<OrderPricingLine> staged)
            => ReversalsOf(originalPricingLineId, staged).Sum(line => line.OriginalAmount);

        private IEnumerable<OrderPricingLine> ReversalsOf(long originalPricingLineId, IReadOnlyList<OrderPricingLine> staged)
            => _pricingLines
                .Concat(staged)
                .Where(line => line.LineRole == PricingLineRole.Reversal
                               && line.OriginalPricingLineId == originalPricingLineId);

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

            var commissionLines = _pricingLines
                .Where(line => line.ComponentType == PricingComponentType.Commission)
                .ToList();

            SetCommission(new Commission(
                commissionLines.LastOrDefault(line => line.UnitPrice.HasValue)?.UnitPrice ?? 0m,
                commissionLines.Sum(line => line.SignedSaleAmount)));
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
