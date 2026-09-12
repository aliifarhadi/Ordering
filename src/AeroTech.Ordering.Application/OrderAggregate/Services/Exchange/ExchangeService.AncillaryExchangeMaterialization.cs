using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.Ports.EmdExchange;
using AeroTech.Ordering.Domain.Servicing.Plans;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed partial class ExchangeService
    {
        private async Task<AcceptedExchangeAncillaryExchangeGroup> MaterializeAncillaryExchangeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            ElectronicMiscDocument source,
            AcceptedExchangeAncillaryExchangeGroup group,
            EmdExchangeResult result,
            CancellationToken cancellationToken)
        {
            var settled = group;
            var successor = result.Successor!;

            var consequenceId = await CommitAncillaryExchangeConsequenceAsync(
                order, operation, settled, cancellationToken);

            if (consequenceId is { } committed)
                settled = settled with { PriceChangeSetId = committed };

            var materialized = await MaterializeSuccessorEmdAsync(
                order, operation, predecessor, source, settled, successor, cancellationToken);

            settled = settled with
            {
                SuccessorElectronicMiscDocumentId = materialized.Id,
                SuccessorDocumentNumber = materialized.DocumentNumber
            };

            await _plans.RecordAncillaryExchangeSuccessorAsync(
                operation.OperationId,
                settled.ExchangeGroupRef,
                materialized.Id,
                materialized.DocumentNumber,
                cancellationToken);

            source.ExchangeCoupons(
                settled.SourceCouponNumbers
                    .Select((couponNumber, index) => new EmdCouponExchange(
                        couponNumber,
                        materialized.Id,
                        materialized.DocumentNumber,
                        successor.Coupons[index].CouponNumber,
                        operation.OperationId,
                        settled.DecisionReference,
                        result.ProviderReference))
                    .ToList(),
                _clock);

            if (result.Residual is { } residual && settled.RequiresDocumentCoupledResidual)
            {
                await MaterializeResidualDocumentAsync(order, operation, predecessor, residual, cancellationToken);

                await _plans.RecordAncillaryExchangeResidualOutcomeAsync(
                    operation.OperationId,
                    settled.ExchangeGroupRef,
                    ProviderOperationOutcome.Confirmed,
                    residual.ProviderReference ?? result.ProviderReference,
                    residual.DocumentNumber,
                    residual.Instrument,
                    null,
                    cancellationToken);

                settled = settled with
                {
                    ResidualOutcome = ProviderOperationOutcome.Confirmed,
                    ResidualProviderReference = residual.ProviderReference ?? result.ProviderReference,
                    ResidualInstrumentReference = residual.DocumentNumber,
                    ResidualInstrument = residual.Instrument
                };
            }

            return settled;
        }

        private async Task<ElectronicMiscDocument> MaterializeSuccessorEmdAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            ElectronicMiscDocument source,
            AcceptedExchangeAncillaryExchangeGroup group,
            SuccessorEmdIdentity successor,
            CancellationToken cancellationToken)
        {
            if (await FindMiscDocumentAsync(order.Id, successor.DocumentNumber, cancellationToken) is { } existing)
                return existing;

            var feePricingLineId = group.PriceChangeSetId is { } changeSetId
                ? order.PricingLines
                    .Where(line => line.PriceChangeSetId == changeSetId)
                    .OrderBy(line => line.Id)
                    .Select(line => (long?)line.Id)
                    .FirstOrDefault()
                : null;

            var issued = ElectronicMiscDocument.IssueProviderConfirmed(
                _idGenerator.NewId(),
                order.Id,
                predecessor.TravelerId,
                operation.OperationId,
                successor.DocumentNumber,
                successor.Type,
                successor.ReasonForIssuanceCode,
                successor.IssuerCarrierId,
                successor.IssuingOfficeId,
                successor.Authority,
                successor.CurrencyId,
                SuccessorCoupons(source, group, feePricingLineId),
                successor.Coupons.Select(coupon => coupon.CouponNumber).ToList(),
                _idGenerator,
                _clock);

            issued.RecordProviderConfirmation(group.ExchangeProviderReference);

            await _miscDocuments.AddAsync(issued, cancellationToken);

            return issued;
        }

        private static IReadOnlyList<EmdCouponIssuance> SuccessorCoupons(
            ElectronicMiscDocument source,
            AcceptedExchangeAncillaryExchangeGroup group,
            long? feePricingLineId)
            => group.SuccessorCoupons
                .Select((coupon, index) =>
                {
                    var replaced = group.SourceCouponNumbers[index];

                    return new EmdCouponIssuance(
                        coupon.Purpose,
                        coupon.ReasonForIssuanceSubCode,
                        coupon.Value,
                        [],
                        OrderServiceId: coupon.Purpose == EmdCouponPurpose.Service ? coupon.OrderServiceId : null,
                        PricingLineId: coupon.Purpose == EmdCouponPurpose.Fee ? feePricingLineId : null,
                        ExternalValueReference: coupon.ExternalValueReference,
                        AssociatedTicketCouponId: group.IsAssociatedSuccessor
                            ? coupon.TargetSuccessorTicketCouponId
                            : null,
                        PredecessorElectronicMiscDocumentId: source.Id,
                        PredecessorDocumentNumber: source.DocumentNumber,
                        PredecessorCouponNumber: replaced);
                })
                .ToList();

        private async Task<long?> CommitAncillaryExchangeConsequenceAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangeAncillaryExchangeGroup group,
            CancellationToken cancellationToken)
        {
            if (group.IsConsequenceCommitted)
                return null;

            var consequence = order.CommitDependentPriceChange(
                new AcceptedDependentPriceChangeArgs(
                    operation.OperationId,
                    OrderChangeType.Exchange,
                    PriceChangeReason.Exchange,
                    group.PricingSource,
                    group.PricingLines.Select(AncillaryExchangePricingLine).ToList(),
                    SourcePricingRef: group.SourceReference),
                _idGenerator,
                _clock);

            await _plans.RecordAncillaryExchangeConsequenceAsync(
                operation.OperationId, group.ExchangeGroupRef, consequence.Id, cancellationToken);

            return consequence.Id;
        }

        private static AcceptedPricingLineArgs AncillaryExchangePricingLine(AcceptedRefundPricingLine line)
            => new(
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
}
