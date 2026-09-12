using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed partial class ExchangeService
    {
        private async Task<ExchangeOutcome> RetainAncillaryResidualAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeAncillaryDisposition pending,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var document = await _miscDocuments.GetAsync(pending.ElectronicMiscDocumentId, cancellationToken)
                           ?? throw ExceptionFactory.ElectronicMiscDocumentNotFound(
                               pending.ElectronicMiscDocumentId);

            if (!document.PermitsResidualRetention(pending.EmdCouponNumber, operation.OperationId))
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            var retained = document.RequireCoupon(pending.EmdCouponNumber);
            var outcome = order.RetainAncillaryResidual(retained.OrderServiceId, _clock);

            if (outcome.IsConflict)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            var settledAt = _clock.GetDateTime();

            await _plans.RecordAncillaryRetentionSettledAsync(
                operation.OperationId, pending.EmdCouponId, settledAt, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = plan.WithAncillaryRetention(pending, settledAt);

            return settled.IsAncillarySettled
                ? await CompleteAsync(
                    order, operation, predecessor, settled, materialized, isReplay, cancellationToken)
                : await ReassociateAncillaryAsync(
                    order, operation, predecessor, settled, successor, materialized,
                    documentJustConfirmed: false, isReplay, cancellationToken);
        }
    }
}
