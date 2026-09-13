using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Ports.DocumentVoid;
using AeroTech.Ordering.Domain.Servicing.Operations;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Plans.Policies;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed partial class ExchangeService
    {
        private async Task<ExchangeOutcome> CancelAncillaryAsync(
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
            if (plan.CancelGroup(pending.CancelGroupRef ?? string.Empty) is not { } group)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            var document = await _miscDocuments.GetAsync(group.ElectronicMiscDocumentId, cancellationToken)
                           ?? throw ExceptionFactory.ElectronicMiscDocumentNotFound(
                               group.ElectronicMiscDocumentId);

            if (!group.IsEligibilityAllowed)
                return await CheckAncillaryCancelEligibilityAsync(
                    order, operation, predecessor, plan, successor, materialized, group, document, isReplay,
                    cancellationToken);

            return group.IsVoidConfirmed
                ? await SettleAncillaryCancellationAsync(
                    order, operation, predecessor, plan, successor, materialized, group, document, isReplay,
                    cancellationToken)
                : await VoidCancelledAncillaryAsync(
                    order, operation, predecessor, plan, successor, materialized, group, document, isReplay,
                    cancellationToken);
        }

        private async Task<ExchangeOutcome> CheckAncillaryCancelEligibilityAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeAncillaryCancelGroup group,
            ElectronicMiscDocument document,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            DocumentVoidEligibility eligibility;

            try
            {
                eligibility = await _documentVoids.CheckEligibilityAsync(
                    new DocumentVoidEligibilityRequest(
                        _keys.AncillaryCancel(operation, group),
                        order.Id,
                        operation.OperationId,
                        AccountableDocumentKind.ElectronicMiscDocument,
                        group.EmdDocumentNumber),
                    cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            await _plans.RecordAncillaryCancelEligibilityAsync(
                operation.OperationId,
                group.CancelGroupRef,
                eligibility.Outcome,
                eligibility.RefundRequiredInstead,
                eligibility.Detail,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var examined = group with
            {
                VoidEligibilityOutcome = eligibility.Outcome,
                VoidRefundRequiredInstead = eligibility.RefundRequiredInstead,
                VoidEligibilityDetail = eligibility.Detail ?? group.VoidEligibilityDetail
            };

            var settled = plan.WithCancelGroup(examined);

            if (eligibility.RefundRequiredInstead || eligibility.Outcome == EligibilityOutcome.Denied)
                return await ReconcileAsync(
                    order, operation, predecessor, settled, isReplay, cancellationToken, materialized);

            if (eligibility.Outcome != EligibilityOutcome.Allowed)
                return await SettleAsync(
                    order, operation, predecessor, settled,
                    ServicingOperationStatus.AwaitingExternal,
                    CommandReceiptStatus.Pending,
                    ExchangeDocumentOutcome.Exchanged,
                    isReplay,
                    cancellationToken,
                    materialized);

            return await VoidCancelledAncillaryAsync(
                order, operation, predecessor, settled, successor, materialized, examined, document, isReplay,
                cancellationToken);
        }

        private async Task<ExchangeOutcome> VoidCancelledAncillaryAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeAncillaryCancelGroup group,
            ElectronicMiscDocument document,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var key = _keys.AncillaryCancel(operation, group);
            var dispatching = group;
            var current = plan;
            DocumentVoidResult result;

            if (!group.WasVoidDispatched)
            {
                if (!document.PermitsCancellationVoid(group.EmdCouponNumbers, operation.OperationId))
                    return await ReconcileAsync(
                        order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

                var dispatchedAt = _clock.GetDateTime();

                await _plans.RecordAncillaryCancelVoidDispatchedAsync(
                    operation.OperationId, group.CancelGroupRef, dispatchedAt, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                dispatching = group with { VoidDispatchedAt = dispatchedAt };
                current = plan.WithCancelGroup(dispatching);
            }

            try
            {
                result = group.WasVoidDispatched
                    ? await _documentVoids.RecoverAsync(
                        new DocumentVoidRecoveryRequest(
                            key,
                            order.Id,
                            operation.OperationId,
                            AccountableDocumentKind.ElectronicMiscDocument,
                            group.EmdDocumentNumber),
                        cancellationToken)
                    : await _documentVoids.VoidAsync(
                        new DocumentVoidRequest(
                            key,
                            order.Id,
                            operation.OperationId,
                            AccountableDocumentKind.ElectronicMiscDocument,
                            group.EmdDocumentNumber,
                            document.IssuerCarrierId),
                        cancellationToken);
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            await _plans.RecordAncillaryCancelVoidOutcomeAsync(
                operation.OperationId,
                dispatching.CancelGroupRef,
                result.Outcome,
                result.ProviderReference,
                result.Detail,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var attempted = dispatching with
            {
                VoidOutcome = result.Outcome,
                VoidProviderReference = result.ProviderReference ?? dispatching.VoidProviderReference,
                VoidDetail = result.Detail ?? dispatching.VoidDetail
            };

            var next = current.WithCancelGroup(attempted);

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

            return await SettleAncillaryCancellationAsync(
                order, operation, predecessor, next, successor, materialized, attempted, document, isReplay,
                cancellationToken);
        }

        private async Task<ExchangeOutcome> SettleAncillaryCancellationAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeAncillaryCancelGroup group,
            ElectronicMiscDocument document,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (!document.IsVoidSettledBy(operation.OperationId))
            {
                if (_callerContext.ActorId is not { } voidedBy
                    || !document.PermitsCancellationVoid(group.EmdCouponNumbers, operation.OperationId)
                    || AncillaryCancellationEvidencePolicy.Conflict(
                        document, group, plan.CancelGroupMembers(group.CancelGroupRef)) is not null)
                    return await ReconcileAsync(
                        order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

                document.Void(
                    operation.OperationId,
                    VoidReason.Other,
                    CancellationVoidDetail(group),
                    voidedBy,
                    group.VoidProviderReference,
                    _idGenerator,
                    _clock);
            }

            if (order.CancelAncillariesByDocumentVoid(group.OrderServiceIds, _clock).IsConflict)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            var settledAt = _clock.GetDateTime();

            await _plans.RecordAncillaryCancellationSettledAsync(
                operation.OperationId, group.CancelGroupRef, settledAt, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = plan.WithCancelGroup(group with { CancellationSettledAt = settledAt });

            return settled.IsAncillarySettled
                ? await CompleteAsync(
                    order, operation, predecessor, settled, materialized, isReplay, cancellationToken)
                : await ReassociateAncillaryAsync(
                    order, operation, predecessor, settled, successor, materialized,
                    documentJustConfirmed: false, isReplay, cancellationToken);
        }

        private static string CancellationVoidDetail(AcceptedExchangeAncillaryCancelGroup group)
            => $"{group.DocumentAction} approved by {group.CancellationReference} on {group.SourceReference}";
    }
}
