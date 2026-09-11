using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.OrderAggregate.ValueObjects;
using AeroTech.Ordering.Domain._Shared.Documents;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public IReadOnlyList<PredecessorPricingEvidence> PredecessorPricingEvidence(
            long predecessorElectronicTicketId,
            string predecessorDocumentNumber,
            IReadOnlyList<CarriedPricingLink> carried)
            => ExchangePricingCorrelation.Evidence(predecessorElectronicTicketId, predecessorDocumentNumber, carried, _pricingLines);

        public TicketedSegmentSnapshot? SoldSegmentSnapshot(long orderServiceId)
        {
            var service = _orderServices.FirstOrDefault(candidate => candidate.Id == orderServiceId);
            var segment = service?.SoldSegmentId is { } segmentId
                ? _segments.FirstOrDefault(candidate => candidate.Id == segmentId)
                : null;

            return segment is null
                ? null
                : new TicketedSegmentSnapshot(
                    segment.MarketingAirlineId,
                    segment.Number,
                    segment.OriginAirportId,
                    segment.DestinationAirportId,
                    segment.DepartureDateTime,
                    segment.ArrivalDateTime,
                    segment.BookingClass);
        }

        public StagedExchange PrepareExchange(AcceptedExchangeArgs args, IIdGenerator idGenerator, IClock clock)
            => StageExchange(args, idGenerator, clock.GetDateTime());

        public ExchangedOrder CommitExchange(StagedExchange staged, IIdGenerator idGenerator, IClock clock)
            => AttachExchange(staged, idGenerator, clock.GetDateTime());

        private StagedExchange StageExchange(AcceptedExchangeArgs args, IIdGenerator idGenerator, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(args);

            var accepted = args.Accepted;

            if (ExchangePricingPolicy.DeferralReason(accepted) is { } unsupported)
                throw ExceptionFactory.ChangeMonetaryOutcomeNotSupported(accepted.QuotedExchangeId, unsupported);

            ExchangePricingPolicy.EnsureWellFormed(accepted);
            EnsureExchangeAllocationsMatch(accepted, args.Coupons);

            var lines = accepted.PricingLines
                .Select(line => MapExchangePricingLine(
                    line, accepted.PredecessorElectronicTicketId, args.PredecessorPricingCorrelation))
                .ToList();

            var changeArgs = new AcceptedPriceChangeArgs(
                OrderChangeType.Exchange,
                PriceChangeReason.Exchange,
                accepted.PricingSource,
                lines,
                SourcePricingRef: accepted.SourcePricingReference,
                ChangeReason: accepted.QuotedExchangeId,
                ExternalReference: accepted.TargetSelectionRef,
                ActorScope: args.ActorScope,
                ActorId: args.ActorId,
                OperationId: args.OperationId);

            var change = StageOrderChange(changeArgs, idGenerator, now);
            var priceChange = StagePriceChange(change, changeArgs, idGenerator, now);

            var coupons = accepted.Coupons
                .OrderBy(coupon => coupon.PredecessorCouponNumber)
                .Select(coupon => StageExchangeCoupon(coupon, args, idGenerator, now))
                .ToList();

            var lineIds = priceChange.Lines.ToDictionary(
                line => line.SourceLineRef!,
                line => line.Id,
                StringComparer.Ordinal);

            return new StagedExchange(priceChange, coupons, args.SuccessorElectronicTicketId, lineIds);
        }

        private StagedExchangeCoupon StageExchangeCoupon(
            AcceptedExchangeCoupon coupon,
            AcceptedExchangeArgs args,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            var allocation = args.Coupons.Single(candidate => candidate.PredecessorTicketCouponId == coupon.PredecessorTicketCouponId);
            var current = RequireChangeableAirService(coupon.PredecessorOrderServiceId);

            EnsureServiceIsHeldForTraveller(current, args.PredecessorTravellerId);

            if (!coupon.IsReplaced)
                return new StagedExchangeCoupon(
                    coupon.PredecessorTicketCouponId,
                    allocation.SuccessorTicketCouponId,
                    ExchangeCouponDisposition.Continued,
                    current.Id,
                    current.SoldSegmentId!.Value,
                    null,
                    null,
                    null);

            var replacement = coupon.Replacement!;

            EnsureNoActiveServiceDependsOn(current.Id);

            if (!replacement.BeneficiaryTravellerIds.ToHashSet().SetEquals([args.PredecessorTravellerId]))
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("traveller");

            var itineraryId = _segments
                .Single(segment => segment.Id == current.SoldSegmentId!.Value)
                .OrderItineraryId;

            var segment = StageReplacementSegment(
                replacement.Segment, allocation.ReplacementOrderSegmentId!.Value, itineraryId, idGenerator);

            var service = StageReplacementService(
                replacement, allocation.ReplacementOrderServiceId!.Value, current, segment.Id, idGenerator, now);

            return new StagedExchangeCoupon(
                coupon.PredecessorTicketCouponId,
                allocation.SuccessorTicketCouponId,
                ExchangeCouponDisposition.Replaced,
                service.Id,
                segment.Id,
                current.Id,
                segment,
                service);
        }

        private static void EnsureExchangeAllocationsMatch(
            AcceptedExchange accepted,
            IReadOnlyList<ExchangeCouponAllocation> allocations)
        {
            if (allocations.Count != accepted.Coupons.Count
                || allocations.Select(allocation => allocation.PredecessorTicketCouponId).Distinct().Count() != allocations.Count
                || allocations.Select(allocation => allocation.SuccessorTicketCouponId).Distinct().Count() != allocations.Count)
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("coupon allocation");

            foreach (var coupon in accepted.Coupons)
            {
                var allocation = allocations.FirstOrDefault(candidate =>
                                     candidate.PredecessorTicketCouponId == coupon.PredecessorTicketCouponId)
                                 ?? throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("coupon allocation");

                var replacementAllocated = allocation.ReplacementOrderServiceId is not null
                                           && allocation.ReplacementOrderSegmentId is not null;

                if (replacementAllocated != coupon.IsReplaced)
                    throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("coupon allocation");
            }
        }

        private static void EnsureServiceIsHeldForTraveller(OrderService service, long travellerId)
        {
            var current = service.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).ToHashSet();

            if (!current.SetEquals([travellerId]))
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("traveller");
        }

        private AcceptedPricingLineArgs MapExchangePricingLine(
            AcceptedExchangePricingLine line,
            long predecessorElectronicTicketId,
            IReadOnlyDictionary<string, long> correlation)
        {
            long? originalId = null;

            if (line.PredecessorCorrelationRef is { } reference)
            {
                if (!correlation.TryGetValue(reference, out var resolved)
                    || _pricingLines.All(candidate => candidate.Id != resolved))
                    throw ExceptionFactory.ExchangeTransferOutsidePredecessorDocument(
                        reference, predecessorElectronicTicketId);

                originalId = resolved;
            }

            return new AcceptedPricingLineArgs(
                line.ComponentType,
                line.Effect,
                line.Direction,
                line.LineRole,
                line.OriginalAmount,
                line.OriginalCurrencyId,
                line.SaleAmount,
                line.SaleCurrencyId,
                line.BasisType,
                line.Refundability,
                OrderItemId: line.OrderItemId,
                Code: line.Code,
                Description: line.Description,
                ExchangeRate: line.ExchangeRate is null ? null : new ExchangeRate(line.ExchangeRate),
                ApplicationLevel: line.ApplicationLevel,
                BasisReferenceId: line.BasisReferenceId,
                SourceLineRef: line.SourceLineRef,
                OccurrenceKey: line.OccurrenceKey,
                OriginalPricingLineId: originalId,
                TransferGroupId: line.TransferGroupId,
                SettlementPartyRef: line.SettlementPartyRef,
                SettlementCategory: line.SettlementCategory);
        }

        private ExchangedOrder AttachExchange(StagedExchange staged, IIdGenerator idGenerator, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(staged);

            var changeSet = AttachPriceChange(staged.PriceChange, now);
            var bindings = new List<ExchangedServiceBinding>();

            foreach (var coupon in staged.Coupons)
            {
                if (coupon.IsReplaced)
                {
                    AddSegment(coupon.ReplacementSegment!);
                    AddOrderService(coupon.ReplacementService!);

                    _orderServices
                        .Single(service => service.Id == coupon.ReplacedOrderServiceId!.Value)
                        .MarkSupersededByExchange();

                    coupon.ReplacementService!.Activate();
                    coupon.ReplacementService.MarkReservationConfirmed();
                    coupon.ReplacementService.MarkDocumented(staged.SuccessorElectronicTicketId, coupon.SuccessorTicketCouponId);
                }
                else
                {
                    _orderServices
                        .Single(service => service.Id == coupon.OrderServiceId)
                        .RebindAccountableDocument(staged.SuccessorElectronicTicketId, coupon.SuccessorTicketCouponId);
                }

                bindings.Add(new ExchangedServiceBinding(
                    coupon.PredecessorTicketCouponId,
                    coupon.SuccessorTicketCouponId,
                    coupon.Disposition,
                    coupon.OrderServiceId,
                    coupon.OrderSegmentId,
                    coupon.ReplacedOrderServiceId));
            }

            RecomputeCommercialSummary();
            IncrementCommercialVersion();

            RaisePricingChanged(staged.PriceChange.Change, changeSet, idGenerator, now);

            return new ExchangedOrder(
                staged.PriceChange.Change.Id,
                changeSet.Id,
                bindings,
                changeSet.FinancialSequence,
                staged.PricingLineIdsBySourceRef);
        }
    }
}
