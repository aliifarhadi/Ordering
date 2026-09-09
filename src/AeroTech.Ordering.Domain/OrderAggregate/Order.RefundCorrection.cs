using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.OrderAggregate
{
    public sealed partial class Order
    {
        public StagedRefundCorrection PrepareRefundCorrection(
            RefundCorrectionArgs args,
            IIdGenerator idGenerator,
            IClock clock)
            => StageRefundCorrection(args, idGenerator, clock.GetDateTime());

        public CorrectedRefund CommitRefundCorrection(
            StagedRefundCorrection staged,
            IIdGenerator idGenerator,
            IClock clock)
            => AttachRefundCorrection(staged, idGenerator, clock.GetDateTime());

        public OrderPriceChangeSet CommittedRefundPriceChangeSet(long priceChangeSetId)
            => _priceChangeSets.FirstOrDefault(set =>
                   set.Id == priceChangeSetId && set.Reason == PriceChangeReason.Refund)
               ?? throw ExceptionFactory.RefundPriceChangeSetNotFound(priceChangeSetId, Id);

        private StagedRefundCorrection StageRefundCorrection(
            RefundCorrectionArgs args,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(args);

            if (string.IsNullOrWhiteSpace(args.Reason))
                throw ExceptionFactory.CancelRefundRequiresReason(Id);

            var refundChangeSet = CommittedRefundPriceChangeSet(args.RefundPriceChangeSetId);

            var refundLines = _pricingLines
                .Where(line => line.PriceChangeSetId == refundChangeSet.Id)
                .OrderBy(line => line.Id)
                .ToList();

            if (refundLines.Count == 0)
                throw ExceptionFactory.RefundPriceChangeSetNotFound(args.RefundPriceChangeSetId, Id);

            var changeArgs = new AcceptedPriceChangeArgs(
                OrderChangeType.CancelRefund,
                PriceChangeReason.Correction,
                PricingSource.OrderingDerived,
                refundLines.Select(line => NegateRefundPricingLine(line, args.OriginalRefundOperationId)).ToList(),
                ChangeReason: args.Reason,
                ExternalReference: args.RefundRecordId.ToString(),
                ActorScope: args.ActorScope,
                ActorId: args.ActorId,
                OperationId: args.OperationId);

            return new StagedRefundCorrection(
                StagePriceChange(changeArgs, idGenerator, now),
                args.RefundRecordId,
                args.RestoredLinks);
        }

        private static AcceptedPricingLineArgs NegateRefundPricingLine(
            OrderPricingLine line,
            long originalRefundOperationId)
            => new(
                line.ComponentType,
                line.Effect,
                PricingComponentPolicy.Opposite(line.Direction),
                PricingLineRole.Adjustment,
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
                Quantity: line.Quantity,
                UnitOfMeasure: line.UnitOfMeasure,
                UnitPrice: line.UnitPrice,
                BasisReferenceId: line.BasisReferenceId,
                OriginalPricingLineId: line.Id,
                RelatedOperationId: originalRefundOperationId,
                SettlementPartyRef: line.SettlementPartyRef,
                SettlementCategory: line.SettlementCategory);

        private CorrectedRefund AttachRefundCorrection(
            StagedRefundCorrection staged,
            IIdGenerator idGenerator,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(staged);

            var changeSet = AttachPriceChange(staged.PriceChange, now);

            RestoreDocumentedServices(staged.RestoredLinks);

            IncrementCommercialVersion();

            RaisePricingChanged(staged.PriceChange.Change, changeSet, idGenerator, now);

            return new CorrectedRefund(
                staged.PriceChange.Change.Id,
                changeSet.Id,
                staged.RefundRecordId,
                staged.RestoredLinks.Select(link => link.OrderServiceId).Distinct().ToList(),
                changeSet.FinancialSequence);
        }

        private void RestoreDocumentedServices(IReadOnlyList<RestoredDocumentLink> links)
        {
            foreach (var link in links)
            {
                var service = _orderServices.FirstOrDefault(candidate => candidate.Id == link.OrderServiceId)
                              ?? throw ExceptionFactory.RefundedServiceNotInOrder(link.OrderServiceId, Id);

                service.RestoreDocumentedAfterRefundCancellation(link.ElectronicTicketId, link.TicketCouponId);
            }

            RecomputeCommercialSummary();
        }
    }
}
