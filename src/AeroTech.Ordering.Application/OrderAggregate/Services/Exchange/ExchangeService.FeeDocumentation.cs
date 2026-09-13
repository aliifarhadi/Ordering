using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Ports.DocumentIssuance;
using AeroTech.Ordering.Domain.Servicing.Operations;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Plans.Policies;
using AeroTech.Ordering.Domain._Shared.Documents;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed partial class ExchangeService
    {
        private async Task<ExchangeOutcome> DocumentServicingFeesAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (plan.NextUnsettledFeeDocument is not { } pending)
                return await AfterFeeDocumentationAsync(
                    order, operation, predecessor, plan, successor, materialized, documentJustConfirmed, isReplay,
                    cancellationToken);

            if (ServicingFeeDocumentResolutionPolicy.Resolve(
                    order.PricingLines, materialized.PriceChangeSetId, pending) is not { } resolved)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            var stock = await _stocks.GetActiveForOperationAsync(
                pending.IssuerCarrierId, _emdDocumentType, operation.OperationId, cancellationToken);

            if (stock is null)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            if (pending.IsIssuanceConfirmed)
                return await MaterializeFeeDocumentAsync(
                    order, operation, predecessor, plan, successor, materialized, resolved, stock,
                    documentJustConfirmed, isReplay, cancellationToken);

            var prior = stock.FindAllocation(operation.OperationId, pending.StockRole);
            var claimed = pending;
            var current = plan;
            DocumentStockAllocation allocation;
            DocumentIssuanceResult result;

            if (prior is null)
            {
                allocation = stock.Allocate(operation.OperationId, pending.StockRole, _idGenerator, _clock);

                await _plans.RecordFeeDocumentAllocationAsync(
                    operation.OperationId, pending.DocumentReference, allocation.DocumentNumber, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                claimed = pending with { AllocatedDocumentNumber = allocation.DocumentNumber };
                current = plan.WithFeeDocument(claimed);
            }
            else
            {
                allocation = prior;
            }

            try
            {
                result = prior is null
                    ? await _emdIssuance.IssueAsync(
                        FeeIssuanceRequest(order, operation, claimed, allocation), cancellationToken)
                    : await _emdIssuance.RecoverAsync(
                        new DocumentRecoveryRequest(
                            _keys.FeeDocument(operation, claimed),
                            order.Id,
                            operation.OperationId,
                            allocation.DocumentNumber),
                        cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            await _plans.RecordFeeDocumentIssuanceOutcomeAsync(
                operation.OperationId,
                claimed.DocumentReference,
                result.Outcome,
                result.ProviderReference,
                result.Detail,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var attempted = claimed.WithIssuanceAttempt(result.Outcome, result.ProviderReference, result.Detail);
            var next = current.WithFeeDocument(attempted);

            if (attempted.IsIssuanceRejected)
                return await ReconcileAsync(
                    order, operation, predecessor, next, isReplay, cancellationToken, materialized);

            if (!attempted.IsIssuanceConfirmed)
                return await SettleAsync(
                    order, operation, predecessor, next,
                    ServicingOperationStatus.AwaitingExternal,
                    result.Outcome == ProviderOperationOutcome.Unknown
                        ? CommandReceiptStatus.Unknown
                        : CommandReceiptStatus.Pending,
                    ExchangeDocumentOutcome.Exchanged,
                    isReplay,
                    cancellationToken,
                    materialized);

            return await MaterializeFeeDocumentAsync(
                order, operation, predecessor, next, successor, materialized,
                resolved with { Document = attempted }, stock, documentJustConfirmed, isReplay, cancellationToken);
        }

        private async Task<ExchangeOutcome> MaterializeFeeDocumentAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            ResolvedServicingFeeDocument resolved,
            DocumentStock stock,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var document = resolved.Document;

            if (document.AllocatedDocumentNumber is not { } documentNumber)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            var existing = await FindMiscDocumentAsync(order.Id, documentNumber, cancellationToken);

            if (existing is not null)
            {
                if (ServicingFeeDocumentEvidencePolicy.Conflict(existing, resolved, operation.OperationId)
                    is not null)
                    return await ReconcileAsync(
                        order, operation, predecessor, plan, isReplay, cancellationToken, materialized);
            }
            else
            {
                existing = ElectronicMiscDocument.Issue(
                    _idGenerator.NewId(),
                    order.Id,
                    document.TravelerId,
                    operation.OperationId,
                    documentNumber,
                    ElectronicMiscDocumentType.Standalone,
                    document.ReasonForIssuanceCode,
                    document.IssuerCarrierId,
                    order.AirlineOfficeId,
                    DocumentAuthority.Local,
                    document.CurrencyId,
                    FeeCouponIssuances(resolved),
                    _idGenerator,
                    _clock);

                existing.RecordProviderConfirmation(document.IssuanceProviderReference);

                await _miscDocuments.AddAsync(existing, cancellationToken);
            }

            if (stock.FindAllocation(operation.OperationId, document.StockRole)
                is { State: StockNumberState.Reserved })
                stock.MarkIssued(operation.OperationId, document.StockRole, _clock);

            var settledAt = _clock.GetDateTime();

            await _plans.RecordFeeDocumentSettledAsync(
                operation.OperationId, document.DocumentReference, existing.Id, settledAt, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = plan.WithFeeDocument(
                document with { ElectronicMiscDocumentId = existing.Id, SettledAt = settledAt });

            return await DocumentServicingFeesAsync(
                order, operation, predecessor, settled, successor, materialized, documentJustConfirmed, isReplay,
                cancellationToken);
        }

        private async Task<ExchangeOutcome> AfterFeeDocumentationAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
            => plan.RequiresAncillaryReassociation && !plan.IsAncillarySettled
                ? await ReassociateAncillaryAsync(
                    order, operation, predecessor, plan, successor, materialized,
                    documentJustConfirmed, isReplay, cancellationToken)
                : await CompleteAsync(
                    order, operation, predecessor, plan, materialized, isReplay, cancellationToken);

        private EmdIssuanceRequest FeeIssuanceRequest(
            Order order,
            OrderOperation operation,
            AcceptedExchangeFeeDocument document,
            DocumentStockAllocation allocation)
        {
            var couponNumber = 1;

            return new EmdIssuanceRequest(
                _keys.FeeDocument(operation, document),
                order.Id,
                operation.OperationId,
                document.TravelerId,
                allocation.DocumentNumber,
                ElectronicMiscDocumentType.Standalone,
                document.ReasonForIssuanceCode,
                document.IssuerCarrierId,
                document.CurrencyId,
                document.TotalAmount,
                document.Coupons
                    .Select(coupon => new EmdCouponRequest(
                        couponNumber++,
                        EmdCouponPurpose.Fee,
                        coupon.ReasonForIssuanceSubCode,
                        coupon.DocumentedAmount))
                    .ToList());
        }

        private static IReadOnlyList<EmdCouponIssuance> FeeCouponIssuances(ResolvedServicingFeeDocument resolved)
            => resolved.Coupons
                .Select(coupon => new EmdCouponIssuance(
                    EmdCouponPurpose.Fee,
                    coupon.Accepted.ReasonForIssuanceSubCode,
                    coupon.Accepted.DocumentedAmount,
                    coupon.PriceLinks,
                    PricingLineId: coupon.PrimaryPricingLineId))
                .ToList();
    }
}
