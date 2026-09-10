using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
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

            var replaced = RequireChangeableAirService(accepted.PredecessorOrderServiceId);

            EnsureNoActiveServiceDependsOn(replaced.Id);
            EnsureExchangeKeepsContinuedServices(accepted);
            EnsureExchangeKeepsTheTraveller(accepted, replaced);

            var lines = accepted.PricingLines
                .Select(line => MapExchangePricingLine(
                    line, accepted.PredecessorElectronicTicketId, args.PredecessorCarriedPricingLineIds))
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

            var itineraryId = _segments
                .Single(segment => segment.Id == replaced.SoldSegmentId!.Value)
                .OrderItineraryId;

            var segment = StageReplacementSegment(
                accepted.Replacement.Segment, args.ReplacementOrderSegmentId, itineraryId, idGenerator);

            var service = StageReplacementService(
                accepted.Replacement, args.ReplacementOrderServiceId, replaced, segment.Id, idGenerator, now);

            var lineIds = priceChange.Lines.ToDictionary(
                line => line.SourceLineRef!,
                line => line.Id,
                StringComparer.Ordinal);

            return new StagedExchange(
                priceChange,
                segment,
                service,
                replaced.Id,
                args.SuccessorElectronicTicketId,
                args.SuccessorTicketCouponId,
                lineIds);
        }

        private void EnsureExchangeKeepsContinuedServices(AcceptedExchange accepted)
        {
            foreach (var continuedId in accepted.ContinuedOrderServiceIds)
            {
                if (continuedId == accepted.PredecessorOrderServiceId)
                    throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("continued service scope");

                if (_orderServices.All(service => service.Id != continuedId))
                    throw ExceptionFactory.ChangeScopeServiceNotInOrder(continuedId, Id);
            }
        }

        private static void EnsureExchangeKeepsTheTraveller(AcceptedExchange accepted, OrderService replaced)
        {
            var current = replaced.Beneficiaries.Select(beneficiary => beneficiary.OrderTravellerId).ToHashSet();

            if (!current.SetEquals(accepted.Replacement.BeneficiaryTravellerIds))
                throw ExceptionFactory.AcceptedExchangeDoesNotMatchTheRequest("traveller");
        }

        private AcceptedPricingLineArgs MapExchangePricingLine(
            AcceptedExchangePricingLine line,
            long predecessorElectronicTicketId,
            IReadOnlyCollection<long> predecessorCarriedPricingLineIds)
        {
            if (line.OriginalPricingLineId is { } originalId)
            {
                if (_pricingLines.All(candidate => candidate.Id != originalId))
                    throw ExceptionFactory.OriginalPricingLineNotFound(originalId);

                if (!predecessorCarriedPricingLineIds.Contains(originalId))
                    throw ExceptionFactory.ExchangeTransferOutsidePredecessorDocument(
                        originalId, predecessorElectronicTicketId);
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
                OriginalPricingLineId: line.OriginalPricingLineId,
                TransferGroupId: line.TransferGroupId,
                SettlementPartyRef: line.SettlementPartyRef,
                SettlementCategory: line.SettlementCategory);
        }

        private ExchangedOrder AttachExchange(StagedExchange staged, IIdGenerator idGenerator, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(staged);

            var changeSet = AttachPriceChange(staged.PriceChange, now);

            AddSegment(staged.Segment);
            AddOrderService(staged.Service);

            _orderServices
                .Single(service => service.Id == staged.ReplacedOrderServiceId)
                .MarkSupersededByExchange();

            staged.Service.Activate();
            staged.Service.MarkReservationConfirmed();
            staged.Service.MarkDocumented(staged.SuccessorElectronicTicketId, staged.SuccessorTicketCouponId);

            RecomputeCommercialSummary();
            IncrementCommercialVersion();

            RaisePricingChanged(staged.PriceChange.Change, changeSet, idGenerator, now);

            return new ExchangedOrder(
                staged.PriceChange.Change.Id,
                changeSet.Id,
                staged.ReplacedOrderServiceId,
                staged.Service.Id,
                staged.Segment.Id,
                changeSet.FinancialSequence,
                staged.PricingLineIdsBySourceRef);
        }
    }
}
