using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Arguments;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.Ports.DocumentExchange;
using AeroTech.Ordering.Domain.Ports.EmdExchange;
using AeroTech.Ordering.Domain.Ports.ExchangeFunding;
using AeroTech.Ordering.Domain.Ports.ExchangeResidual;
using AeroTech.Ordering.Domain.Servicing.Operations;
using AeroTech.Ordering.Domain.Servicing.Plans;
using AeroTech.Ordering.Domain.Servicing.Plans.Policies;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Exchange
{
    public sealed partial class ExchangeService
    {
        private async Task<ExchangeOutcome> ExchangeAncillaryToNewEmdAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeAncillaryDisposition pending,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            if (plan.ExchangeGroup(pending.ExchangeGroupRef ?? string.Empty) is not { } group)
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            if (group.RequiresFunding && !group.IsFundingGuaranteed)
                return await GuaranteeAncillaryExchangeFundingAsync(
                    order, operation, predecessor, plan, successor, materialized, group, documentJustConfirmed,
                    isReplay, cancellationToken);

            if (!group.IsExchangeConfirmed)
                return await DispatchAncillaryExchangeAsync(
                    order, operation, predecessor, plan, successor, materialized, group, documentJustConfirmed,
                    isReplay, cancellationToken);

            if (group.RequiresFunding && !group.IsFundingCaptured)
                return await CaptureAncillaryExchangeFundingAsync(
                    order, operation, predecessor, plan, successor, materialized, group, isReplay, cancellationToken);

            if (group.RequiresRefundDue && !group.IsRefundDueSettled)
                return await SettleAncillaryExchangeRefundDueAsync(
                    order, operation, predecessor, plan, successor, materialized, group, isReplay, cancellationToken);

            if (group.RequiresExternalResidual && !group.IsResidualSettled)
                return await SettleAncillaryExchangeResidualAsync(
                    order, operation, predecessor, plan, successor, materialized, group, isReplay, cancellationToken);

            return plan.IsAncillarySettled
                ? await CompleteAsync(
                    order, operation, predecessor, plan, materialized, isReplay, cancellationToken)
                : await ReassociateAncillaryAsync(
                    order, operation, predecessor, plan, successor, materialized,
                    documentJustConfirmed: false, isReplay, cancellationToken);
        }

        private async Task<ExchangeOutcome> DispatchAncillaryExchangeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket predecessor,
            AcceptedExchangePlan plan,
            SuccessorDocumentIdentity successor,
            MaterializedExchange materialized,
            AcceptedExchangeAncillaryExchangeGroup group,
            bool documentJustConfirmed,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            var source = await _miscDocuments.GetAsync(group.SourceElectronicMiscDocumentId, cancellationToken)
                         ?? throw ExceptionFactory.ElectronicMiscDocumentNotFound(
                             group.SourceElectronicMiscDocumentId);

            if (group.SourceCouponNumbers.Any(couponNumber =>
                    !source.PermitsExchange(couponNumber, operation.OperationId)))
                return await ReconcileAsync(
                    order, operation, predecessor, plan, isReplay, cancellationToken, materialized);

            var key = _keys.AncillaryExchange(operation, group);
            EmdExchangeResult result;

            try
            {
                if (documentJustConfirmed)
                {
                    result = await DispatchEmdExchangeAsync(
                        order, operation, group, successor, predecessor, key, cancellationToken);
                }
                else
                {
                    var recovered = await _emdExchanges.RecoverAsync(
                        new EmdExchangeRecoveryRequest(
                            key, order.Id, operation.OperationId, group.SourceDocumentNumber),
                        cancellationToken);

                    result = recovered.WasDispatched
                        ? recovered.AsResult()
                        : await DispatchEmdExchangeAsync(
                            order, operation, group, successor, predecessor, key, cancellationToken);
                }
            }
            catch
            {
                await MarkAwaitingExternalAsync(operation);
                throw;
            }

            var confirmed = result.Outcome == ProviderOperationOutcome.Confirmed;

            var contradiction = confirmed
                ? AncillaryExchangeEvidencePolicy.Contradiction(
                      group,
                      successor.DocumentNumber,
                      ExpectedSuccessorTicketCouponNumbers(group, successor),
                      predecessor.TravelerId,
                      result)
                  ?? await SuccessorEmdConflictAsync(
                      order, operation, group, result.Successor!, predecessor.TravelerId, cancellationToken)
                  ?? await ResidualEmdConflictAsync(order, result.Residual, cancellationToken)
                : null;

            await _plans.RecordAncillaryExchangeOutcomeAsync(
                operation.OperationId,
                group.ExchangeGroupRef,
                result.Outcome,
                result.ProviderReference,
                contradiction ?? result.Detail,
                cancellationToken);

            var settledGroup = group with
            {
                ExchangeOutcome = result.Outcome,
                ExchangeProviderReference = result.ProviderReference ?? group.ExchangeProviderReference,
                ExchangeDetail = contradiction ?? result.Detail ?? group.ExchangeDetail
            };

            if (confirmed && contradiction is null)
                settledGroup = await MaterializeAncillaryExchangeAsync(
                    order, operation, predecessor, plan, source, settledGroup, result, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var settled = plan.WithExchangeGroup(settledGroup);

            if (contradiction is not null || result.Outcome == ProviderOperationOutcome.Rejected)
                return await ReconcileAsync(
                    order, operation, predecessor, settled, isReplay, cancellationToken, materialized);

            if (!confirmed)
                return await SettleAsync(
                    order, operation, predecessor, settled,
                    ServicingOperationStatus.AwaitingExternal,
                    result.Outcome == ProviderOperationOutcome.Unknown
                        ? CommandReceiptStatus.Unknown
                        : CommandReceiptStatus.Pending,
                    ExchangeDocumentOutcome.Exchanged,
                    isReplay,
                    cancellationToken,
                    materialized);

            return await ExchangeAncillaryToNewEmdAsync(
                order, operation, predecessor, settled, successor, materialized,
                settled.Ancillaries.First(disposition =>
                    disposition.IsEmdExchange
                    && disposition.ExchangeGroupRef == settledGroup.ExchangeGroupRef),
                documentJustConfirmed: false,
                isReplay,
                cancellationToken);
        }

        private async Task<EmdExchangeResult> DispatchEmdExchangeAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangeAncillaryExchangeGroup group,
            SuccessorDocumentIdentity successor,
            ElectronicTicket predecessor,
            string operationKey,
            CancellationToken cancellationToken)
            => await _emdExchanges.ExchangeAsync(
                new EmdExchangeRequest(
                    operationKey,
                    order.Id,
                    operation.OperationId,
                    group.ExchangeGroupRef,
                    group.SourceDocumentNumber,
                    group.SourceCouponNumbers,
                    predecessor.TravelerId,
                    group.SuccessorType,
                    group.SuccessorReasonForIssuanceCode,
                    group.CurrencyId,
                    SuccessorCouponRequests(group, successor),
                    group.IsAssociatedSuccessor ? successor.DocumentNumber : null,
                    group.DecisionReference,
                    group.SourceReference,
                    group.RequiresDocumentCoupledResidual
                        ? new ExchangeCoupledResidualRequest(
                            group.Residual!.Amount,
                            group.Residual.CurrencyId,
                            group.Residual.Disposition,
                            group.Residual.ExpectedInstrument)
                        : null),
                cancellationToken);

        private static IReadOnlyList<EmdExchangeSuccessorCouponRequest> SuccessorCouponRequests(
            AcceptedExchangeAncillaryExchangeGroup group,
            SuccessorDocumentIdentity successor)
        {
            var expected = ExpectedSuccessorTicketCouponNumbers(group, successor);

            return group.SuccessorCoupons
                .Select((coupon, index) => new EmdExchangeSuccessorCouponRequest(
                    coupon.Purpose,
                    coupon.ReasonForIssuanceSubCode,
                    coupon.Value,
                    expected[index]))
                .ToList();
        }

        private static IReadOnlyList<int?> ExpectedSuccessorTicketCouponNumbers(
            AcceptedExchangeAncillaryExchangeGroup group,
            SuccessorDocumentIdentity successor)
            => group.SuccessorCoupons
                .Select(coupon => coupon.TargetPredecessorCouponNumber is { } target
                    ? SuccessorCouponNumberOf(successor, target)
                    : null)
                .ToList();

        private static int? SuccessorCouponNumberOf(SuccessorDocumentIdentity successor, int predecessorCouponNumber)
            => successor.Coupons
                .FirstOrDefault(coupon => coupon.PredecessorCouponNumber == predecessorCouponNumber)
                ?.CouponNumber;

        private async Task<string?> SuccessorEmdConflictAsync(
            Order order,
            OrderOperation operation,
            AcceptedExchangeAncillaryExchangeGroup group,
            SuccessorEmdIdentity successor,
            long? beneficiaryTravellerId,
            CancellationToken cancellationToken)
        {
            var existing = await FindMiscDocumentAsync(order.Id, successor.DocumentNumber, cancellationToken);

            return existing is null
                ? null
                : ElectronicMiscDocumentIdentityPolicy.Conflict(
                    existing, group, successor, operation.OperationId, beneficiaryTravellerId);
        }

        private async Task<string?> ResidualEmdConflictAsync(
            Order order,
            Domain.Ports.DocumentExchange.ResidualDocumentIdentity? residual,
            CancellationToken cancellationToken)
        {
            if (residual is null)
                return null;

            var existing = await FindMiscDocumentAsync(order.Id, residual.DocumentNumber, cancellationToken);

            return existing is null || existing.IsResidualValueDocumentFor(residual.Amount, residual.CurrencyId)
                ? null
                : $"miscellaneous document {residual.DocumentNumber} already exists and is not "
                  + "the coupled residual document this exchange reported";
        }
    }
}
