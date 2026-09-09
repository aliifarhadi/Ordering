using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public StagedRefund PrepareRefund(AcceptedRefundArgs args, IIdGenerator idGenerator, IClock clock)
            => StageRefund(args, idGenerator, clock.GetDateTime());

        public RefundedDocument CommitRefund(StagedRefund staged, IIdGenerator idGenerator, IClock clock)
            => AttachRefund(staged, idGenerator, clock.GetDateTime());

        public void ApplyDocumentRefund(IReadOnlyCollection<long> orderServiceIds, IClock clock)
        {
            MarkServicesRefunded(orderServiceIds);

            _ = clock;
        }

        public void EnsureRefundScopeBelongsToTheOrder(IReadOnlyCollection<long> orderServiceIds)
        {
            foreach (var serviceId in orderServiceIds)
            {
                if (_orderServices.All(service => service.Id != serviceId))
                    throw ExceptionFactory.RefundedServiceNotInOrder(serviceId, Id);
            }
        }

        private void MarkServicesRefunded(IReadOnlyCollection<long> orderServiceIds)
        {
            foreach (var service in _orderServices.Where(service => orderServiceIds.Contains(service.Id)))
                service.MarkDocumentRefunded();

            RecomputeCommercialSummary();
        }

        private StagedRefund StageRefund(AcceptedRefundArgs args, IIdGenerator idGenerator, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(args);

            var accepted = args.Accepted;

            if (accepted.PricingSource == PricingSource.OrderingDerived)
                throw ExceptionFactory.RefundPricingSourceNotAllowed(accepted.PricingSource);

            if (accepted.PricingLines.Count == 0)
                throw ExceptionFactory.RefundRequiresPricingLines(accepted.QuotedRefundId);

            if (accepted.ApprovedRefundAmount < 0m)
                throw ExceptionFactory.RefundAmountMustBeNonNegative(accepted.ApprovedRefundAmount);

            RefundConservationPolicy.EnsureReconciles(accepted.PricingLines, accepted.ApprovedRefundAmount);

            var serviceIds = args.RefundedOrderServiceIds.Distinct().ToList();

            EnsureRefundScopeBelongsToTheOrder(serviceIds);

            var lines = accepted.PricingLines
                .Select(line => MapRefundPricingLine(line, args.DocumentPricingLineIds))
                .ToList();

            var changeArgs = new AcceptedPriceChangeArgs(
                OrderChangeType.Refund,
                PriceChangeReason.Refund,
                accepted.PricingSource,
                lines,
                SourcePricingRef: accepted.SourcePricingReference,
                ChangeReason: accepted.QuotedRefundId,
                ExternalReference: accepted.QuotedRefundId,
                ActorScope: args.ActorScope,
                ActorId: args.ActorId,
                OperationId: args.OperationId);

            return new StagedRefund(
                StagePriceChange(changeArgs, idGenerator, now),
                serviceIds,
                accepted.ApprovedRefundAmount);
        }

        private AcceptedPricingLineArgs MapRefundPricingLine(
            AcceptedRefundPricingLine line,
            IReadOnlyCollection<long> documentPricingLineIds)
        {
            if (line.LineRole == PricingLineRole.Reversal && line.ReversesPricingLineId is null)
                throw ExceptionFactory.ReversalRequiresOriginalLine();

            if (line.ReversesPricingLineId is { } originalId)
            {
                if (_pricingLines.All(candidate => candidate.Id != originalId))
                    throw ExceptionFactory.OriginalPricingLineNotFound(originalId);

                if (!documentPricingLineIds.Contains(originalId))
                    throw ExceptionFactory.RefundReversalOutsideDocumentScope(originalId, Id);
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
                ExchangeRate: line.ExchangeRate,
                ApplicationLevel: line.ApplicationLevel,
                BasisReferenceId: line.BasisReferenceId,
                SourceLineRef: line.SourceLineRef,
                OccurrenceKey: line.OccurrenceKey,
                OriginalPricingLineId: line.ReversesPricingLineId,
                SettlementPartyRef: line.SettlementPartyRef,
                SettlementCategory: line.SettlementCategory);
        }

        private RefundedDocument AttachRefund(StagedRefund staged, IIdGenerator idGenerator, DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(staged);

            var changeSet = AttachPriceChange(staged.PriceChange, now);

            MarkServicesRefunded(staged.ServiceIds);

            IncrementCommercialVersion();

            RaisePricingChanged(staged.PriceChange.Change, changeSet, idGenerator, now);

            return new RefundedDocument(
                staged.PriceChange.Change.Id,
                changeSet.Id,
                staged.ServiceIds,
                changeSet.FinancialSequence);
        }
    }
}
