using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.DocumentStockAggregate;
using AeroTech.Ordering.Domain.DocumentStockAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Exchange;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
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

            if (ResolvePrimaryPricingLines(order, materialized, pending) is not { } primaryPricingLineIds)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            var stock = await _stocks.GetActiveForOperationAsync(
                pending.IssuerCarrierId, _emdDocumentType, operation.OperationId, cancellationToken);

            if (stock is null)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

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

            var attempted = claimed with
            {
                IssuanceOutcome = result.Outcome,
                IssuanceProviderReference = result.ProviderReference ?? claimed.IssuanceProviderReference,
                IssuanceDetail = result.Detail ?? claimed.IssuanceDetail
            };

            var next = current.WithFeeDocument(attempted);

            if (result.Outcome == ProviderOperationOutcome.Rejected)
                return await ReconcileAsync(
                    order, operation, predecessor, next, isReplay, cancellationToken, materialized);

            if (result.Outcome != ProviderOperationOutcome.Confirmed)
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
                order, operation, predecessor, next, successor, materialized, attempted, stock,
                primaryPricingLineIds, documentJustConfirmed, isReplay, cancellationToken);
        }

        private async Task<ExchangeOutcome> MaterializeFeeDocumentAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeFeeDocument document,
            DocumentStock stock,
            IReadOnlyList<long> primaryPricingLineIds,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var existing = await FindMiscDocumentAsync(
                order.Id, document.AllocatedDocumentNumber!, cancellationToken);

            if (existing is not null)
            {
                if (ServicingFeeDocumentEvidencePolicy.Conflict(
                        existing, document, operation.OperationId, primaryPricingLineIds) is not null)
                    return await ReconcileAsync(
                        order, operation, predecessor, plan, isReplay, cancellationToken, materialized);
            }
            else
            {
                if (ResolvePriceLinks(order, materialized, document) is not { } coupons)
                    return await ReconcileAsync(
                        order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

                existing = ElectronicMiscDocument.Issue(
                    _idGenerator.NewId(),
                    order.Id,
                    document.TravelerId,
                    operation.OperationId,
                    document.AllocatedDocumentNumber!,
                    ElectronicMiscDocumentType.Standalone,
                    document.ReasonForIssuanceCode,
                    document.IssuerCarrierId,
                    order.AirlineOfficeId,
                    DocumentAuthority.Local,
                    document.CurrencyId,
                    coupons,
                    _idGenerator,
                    _clock);

                existing.RecordProviderConfirmation(document.IssuanceProviderReference);

                await _miscDocuments.AddAsync(existing, cancellationToken);
            }

            if (stock.FindAllocation(operation.OperationId, document.StockRole) is { State: StockNumberState.Reserved })
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

        private static IReadOnlyList<long>? ResolvePrimaryPricingLines(
            Order order,
            MaterializedExchange materialized,
            AcceptedExchangeFeeDocument document)
        {
            var resolved = new List<long>();

            foreach (var coupon in document.Coupons)
            {
                if (CommittedLine(order, materialized, coupon.PrimarySourceLineRef) is not { } line)
                    return null;

                resolved.Add(line.Id);
            }

            return resolved;
        }

        private static IReadOnlyList<EmdCouponIssuance>? ResolvePriceLinks(
            Order order,
            MaterializedExchange materialized,
            AcceptedExchangeFeeDocument document)
        {
            var coupons = new List<EmdCouponIssuance>();

            foreach (var coupon in document.Coupons)
            {
                if (CommittedLine(order, materialized, coupon.PrimarySourceLineRef) is not { } primary)
                    return null;

                var links = new List<EmdCouponPriceLink>();

                foreach (var attribution in coupon.Attributions)
                {
                    if (CommittedLine(order, materialized, attribution.SourceLineRef) is not { } line)
                        return null;

                    links.Add(new EmdCouponPriceLink(line.Id, null, attribution.AttributedAmount));
                }

                coupons.Add(new EmdCouponIssuance(
                    EmdCouponPurpose.Fee,
                    coupon.ReasonForIssuanceSubCode,
                    coupon.DocumentedAmount,
                    links,
                    PricingLineId: primary.Id));
            }

            return coupons;
        }

        private static OrderPricingLine? CommittedLine(
            Order order,
            MaterializedExchange materialized,
            string sourceLineRef)
        {
            var matches = order.PricingLines
                .Where(line => line.PriceChangeSetId == materialized.PriceChangeSetId
                               && string.Equals(line.SourceLineRef, sourceLineRef, StringComparison.Ordinal))
                .ToList();

            return matches.Count == 1 ? matches[0] : null;
        }
    }
}
