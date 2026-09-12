using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.Servicing.Operations;
using AeroTech.Ordering.Domain.Servicing.Plans;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public static class ExchangeOutcomeFactory
    {
        public static IReadOnlyList<ExchangeMonetaryLegOutcome> MonetaryLegsOf(AcceptedExchangePlan plan)
            => plan.MonetaryLegs
                .Select(leg => new ExchangeMonetaryLegOutcome(
                    leg.Kind,
                    leg.LegIdentity,
                    leg.Amount,
                    leg.CurrencyId,
                    leg.Disposition,
                    plan.LegState(leg.Kind),
                    plan.LegProviderReference(leg.Kind),
                    leg.Kind == ExchangeMonetaryLegKind.Residual ? plan.ResidualInstrumentReference : null,
                    leg.Kind == ExchangeMonetaryLegKind.Residual ? plan.ResidualInstrument : null))
                .ToList();

        public static IReadOnlyList<ExchangeAncillaryOutcome> AncillariesOf(AcceptedExchangePlan plan)
            => plan.Ancillaries
                .Select(disposition => new ExchangeAncillaryOutcome(
                    disposition.LegIdentity,
                    disposition.ElectronicMiscDocumentId,
                    disposition.EmdDocumentNumber,
                    disposition.EmdCouponNumber,
                    disposition.PredecessorCouponNumber,
                    disposition.TargetPredecessorCouponNumber,
                    disposition.Disposition,
                    disposition.State,
                    disposition.DecisionReference,
                    disposition.DecisionVersion,
                    disposition.AssociationProviderReference,
                    disposition.AssociationDetail))
                .ToList();

        public static ExchangeOutcome Create(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            ElectronicTicket? successor,
            long? orderChangeId,
            long? priceChangeSetId,
            ServicingOperationStatus operationStatus,
            ExchangeDocumentOutcome documentOutcome,
            bool isReplay)
            => new(
                order.Id,
                operation.OperationId,
                OrderChangeType.Exchange,
                ServicingOperationKind.Exchange,
                predecessor.Id,
                predecessor.DocumentNumber,
                predecessor.DocumentVersion,
                successor?.Id,
                successor?.DocumentNumber ?? plan.Successor?.DocumentNumber,
                successor?.DocumentVersion,
                plan.ChangedOrderServiceIds,
                plan.Coupons
                    .Select(coupon => new ExchangeCouponOutcome(
                        coupon.PredecessorTicketCouponId,
                        coupon.PredecessorCouponNumber,
                        coupon.Disposition,
                        successor is null ? coupon.PredecessorOrderServiceId : coupon.ServiceAfterExchange,
                        successor is null || !coupon.IsReplaced ? null : coupon.PredecessorOrderServiceId,
                        successor is null ? null : coupon.SuccessorTicketCouponId,
                        successor is null ? null : SuccessorCouponAttribution.Host(plan, coupon)))
                    .ToList(),
                orderChangeId,
                priceChangeSetId,
                order.CommercialVersion,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                plan.EligibilityOutcome,
                plan.ReservationOutcome,
                plan.DocumentExchangeOutcome ?? ProviderOperationOutcome.Pending,
                plan.DocumentExchangeProviderReference,
                documentOutcome,
                operationStatus,
                plan.MonetaryOutcome,
                plan.AddCollect?.Amount,
                plan.AddCollect?.CurrencyId,
                plan.FundingState,
                plan.FundingCaptureReference ?? plan.FundingGuaranteeReference,
                plan.MonetaryAmount,
                plan.MonetaryCurrencyId,
                plan.MonetaryDisposition,
                plan.MonetaryState,
                plan.MonetaryProviderReference,
                plan.ResidualInstrumentReference,
                plan.ResidualInstrument,
                MonetaryLegsOf(plan),
                plan.AncillaryState,
                AncillariesOf(plan),
                operationStatus == ServicingOperationStatus.NeedsReconciliation,
                plan.Disposition == AcceptedExchangeDisposition.DeferredToExpandedExchange,
                plan.Disposition == AcceptedExchangeDisposition.DeferredToExpandedExchange
                    ? plan.DispositionDetail
                    : null,
                isReplay);
    }
}
