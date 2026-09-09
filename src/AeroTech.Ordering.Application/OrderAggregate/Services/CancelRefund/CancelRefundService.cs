using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Application.OrderAggregate.Services.Refund;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.Ports.DocumentRefundCorrection;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Operations;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using Entities = AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.CancelRefund
{
    public sealed class CancelRefundService : ICancelRefundService
    {
        public const string CorrectionStep = "cancel-refund";

        private readonly IOrderRepository _orders;
        private readonly IElectronicTicketRepository _tickets;
        private readonly IDocumentRefundCorrectionPort _documents;
        private readonly IRefundValueCorrectionCoordinator _valueCorrection;
        private readonly ICancelRefundAuthorizer _authorizer;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IServicingOperationStore _operationStore;
        private readonly ICommandReceiptStore _receipts;
        private readonly ICallerContext _callerContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;

        public CancelRefundService(
            IOrderRepository orders,
            IElectronicTicketRepository tickets,
            IDocumentRefundCorrectionPort documents,
            IRefundValueCorrectionCoordinator valueCorrection,
            ICancelRefundAuthorizer authorizer,
            IOrderOperationCoordinator operations,
            IServicingOperationStore operationStore,
            ICommandReceiptStore receipts,
            ICallerContext callerContext,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock,
            IOrderProjector projector)
        {
            _orders = orders;
            _tickets = tickets;
            _documents = documents;
            _valueCorrection = valueCorrection;
            _authorizer = authorizer;
            _operations = operations;
            _operationStore = operationStore;
            _receipts = receipts;
            _callerContext = callerContext;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
            _projector = projector;
        }

        public async Task<CancelRefundOutcome> CancelRefundAsync(
            CancelRefundExecution execution,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            ArgumentException.ThrowIfNullOrWhiteSpace(execution.IdempotencyKey);

            if (string.IsNullOrWhiteSpace(execution.Reason))
                throw ExceptionFactory.CancelRefundRequiresReason(execution.OrderId);

            if (execution.ExpectedCommercialVersion is null)
                throw ExceptionFactory.ExpectedCommercialVersionRequired(execution.OrderId);

            var order = await _orders.GetAsync(execution.OrderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(execution.OrderId);

            var ticket = await ResolveAsync(execution.OrderId, execution.ElectronicTicketId, cancellationToken);
            var refund = ticket.RefundRecord(execution.RefundRecordId);

            var operation = await _operations.BeginAsync(
                execution.OrderId,
                ServicingOperationKind.CancelRefund,
                execution.IdempotencyKey,
                new
                {
                    Operation = "CancelRefund",
                    OrderId = execution.OrderId,
                    ElectronicTicketId = execution.ElectronicTicketId,
                    RefundRecordId = execution.RefundRecordId,
                    Reason = execution.Reason,
                    ReasonDetail = execution.ReasonDetail,
                    ExpectedCommercialVersion = execution.ExpectedCommercialVersion
                },
                execution.ExpectedCommercialVersion,
                cancellationToken);

            var committed = CommittedCorrection(order, operation.OperationId);

            if (committed is not null)
                return await ReplayFinalizedAsync(order, operation, ticket, refund, committed, cancellationToken);

            if (operation.IsReplay)
            {
                var unfinished = await ReplayUnfinishedAsync(
                    order, operation, ticket, refund, execution, cancellationToken);

                if (unfinished is not null)
                    return unfinished;
            }

            ProviderOperationOutcome outcome;
            string? providerReference;
            StagedRefundCorrection staged;

            try
            {
                if (execution.ExpectedCommercialVersion != order.CommercialVersion)
                    throw ExceptionFactory.OrderCommercialVersionMismatch(
                        execution.ExpectedCommercialVersion,
                        execution.OrderId,
                        order.CommercialVersion);

                ticket.EnsureRefundCanBeCancelled(refund.Id);

                await _authorizer.AuthorizeAsync(order, operation, ticket, refund, execution, cancellationToken);

                await _operationStore.TransitionAsync(
                    operation.OperationId,
                    ServicingOperationStatus.Executing,
                    operation.ClaimGeneration,
                    cancellationToken);

                var eligibility = await _documents.CheckEligibilityAsync(
                    new DocumentRefundCorrectionEligibilityRequest(
                        CorrectionKey(operation, refund),
                        execution.OrderId,
                        operation.OperationId,
                        ticket.DocumentNumber,
                        refund.Id,
                        refund.QuotedRefundId,
                        ticket.RefundedCouponNumbers(refund.Id)),
                    cancellationToken);

                if (eligibility.Outcome == EligibilityOutcome.Denied)
                    return await RefuseAsync(order, operation, ticket, refund, cancellationToken);

                if (eligibility.Outcome == EligibilityOutcome.PendingEvidence)
                    return await SuspendAsync(
                        order, operation, ticket, refund, ProviderOperationOutcome.Pending, cancellationToken);

                staged = PrepareCorrection(order, operation, ticket, refund, execution);

                var result = await _documents.CancelRefundAsync(
                    new DocumentRefundCorrectionRequest(
                        CorrectionKey(operation, refund),
                        execution.OrderId,
                        operation.OperationId,
                        ticket.DocumentNumber,
                        refund.Id,
                        refund.QuotedRefundId,
                        ticket.RefundedCouponNumbers(refund.Id),
                        execution.Reason,
                        execution.ReasonDetail),
                    cancellationToken);

                outcome = result.Outcome;
                providerReference = result.ProviderReference;
            }
            catch
            {
                await TryReleaseRejectedAsync(execution.OrderId, operation, cancellationToken);
                throw;
            }

            return outcome switch
            {
                ProviderOperationOutcome.Confirmed => await FinalizeAsync(
                    order, operation, ticket, refund, execution, staged,
                    providerReference, RefundValueDispatch.FirstAttempt, cancellationToken),
                ProviderOperationOutcome.Rejected => await RejectAsync(order, operation, ticket, refund, cancellationToken),
                _ => await SuspendAsync(order, operation, ticket, refund, outcome, cancellationToken)
            };
        }

        private StagedRefundCorrection PrepareCorrection(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            CancelRefundExecution execution)
            => order.PrepareRefundCorrection(
                new RefundCorrectionArgs(
                    refund.Id,
                    refund.OperationId,
                    refund.PriceChangeSetId ?? throw ExceptionFactory.RefundPriceChangeSetNotFound(0, order.Id),
                    refund.Coupons
                        .Select(coupon => new RestoredDocumentLink(
                            coupon.OrderServiceId, ticket.Id, coupon.TicketCouponId))
                        .ToList(),
                    operation.OperationId,
                    execution.Reason,
                    _callerContext.ActorId,
                    CallerScope.For(_callerContext)),
                _idGenerator,
                _clock);

        private async Task<CancelRefundOutcome> FinalizeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            CancelRefundExecution execution,
            StagedRefundCorrection staged,
            string? providerReference,
            RefundValueDispatch dispatch,
            CancellationToken cancellationToken)
        {
            var correction = ticket.CancelRefund(
                refund.Id,
                operation.OperationId,
                execution.Reason,
                execution.ReasonDetail,
                providerReference,
                _callerContext.ActorId,
                CallerScope.For(_callerContext),
                _idGenerator,
                _clock);

            var corrected = order.CommitRefundCorrection(staged, _idGenerator, _clock);

            ticket.AttachRefundCorrectionPriceChangeSet(operation.OperationId, corrected.PriceChangeSetId);

            var valueOutcome = await _valueCorrection.SettleAsync(
                order.Id, operation, ticket, refund, dispatch, cancellationToken);

            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Completed,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.Completed, cancellationToken);
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);

            await _projector.ProjectAsync(order.Id, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(
                order, operation, ticket, refund, correction, corrected,
                ProviderOperationOutcome.Confirmed, valueOutcome,
                ServicingOperationStatus.Completed, correctionNotAvailable: false, isReplay: false);
        }

        private async Task<CancelRefundOutcome> RejectAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            CancellationToken cancellationToken)
            => await SettleUnfinishedAsync(
                order, operation, ticket, refund,
                ServicingOperationStatus.Rejected,
                CommandReceiptStatus.Rejected,
                ProviderOperationOutcome.Rejected,
                releaseClaim: true, correctionNotAvailable: false, isReplay: false, cancellationToken);

        private async Task<CancelRefundOutcome> RefuseAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            CancellationToken cancellationToken)
            => await SettleUnfinishedAsync(
                order, operation, ticket, refund,
                ServicingOperationStatus.Rejected,
                CommandReceiptStatus.Rejected,
                ProviderOperationOutcome.Rejected,
                releaseClaim: true, correctionNotAvailable: true, isReplay: false, cancellationToken);

        private async Task<CancelRefundOutcome> SuspendAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            ProviderOperationOutcome outcome,
            CancellationToken cancellationToken)
            => await SettleUnfinishedAsync(
                order, operation, ticket, refund,
                ServicingOperationStatus.AwaitingExternal,
                outcome == ProviderOperationOutcome.Unknown
                    ? CommandReceiptStatus.Unknown
                    : CommandReceiptStatus.Pending,
                outcome,
                releaseClaim: false, correctionNotAvailable: false, isReplay: false, cancellationToken);

        private async Task<CancelRefundOutcome> ReconcileAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            ProviderOperationOutcome outcome,
            CancellationToken cancellationToken)
            => await SettleUnfinishedAsync(
                order, operation, ticket, refund,
                ServicingOperationStatus.NeedsReconciliation,
                CommandReceiptStatus.NeedsReconciliation,
                outcome,
                releaseClaim: false, correctionNotAvailable: false, isReplay: true, cancellationToken);

        private async Task<CancelRefundOutcome> SettleUnfinishedAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            ServicingOperationStatus operationStatus,
            CommandReceiptStatus receiptStatus,
            ProviderOperationOutcome documentOutcome,
            bool releaseClaim,
            bool correctionNotAvailable,
            bool isReplay,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                operationStatus,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, receiptStatus, cancellationToken);

            if (releaseClaim)
                await _operations.ResolveAsync(order.Id, operation, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(
                order, operation, ticket, refund, null, null,
                documentOutcome, ProviderOperationOutcome.Pending,
                operationStatus, correctionNotAvailable, isReplay);
        }

        private async Task<CancelRefundOutcome?> ReplayUnfinishedAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            CancelRefundExecution execution,
            CancellationToken cancellationToken)
        {
            var prior = await _operationStore.FindAsync(operation.OperationId, cancellationToken);

            if (prior is null)
                return null;

            if (prior.Status == ServicingOperationStatus.Rejected)
                return Outcome(
                    order, operation, ticket, refund, null, null,
                    ProviderOperationOutcome.Rejected, ProviderOperationOutcome.Pending,
                    prior.Status, correctionNotAvailable: false, isReplay: true);

            if (prior.Status is not (ServicingOperationStatus.Executing
                or ServicingOperationStatus.AwaitingExternal
                or ServicingOperationStatus.NeedsReconciliation))
                return null;

            var recovery = await _documents.RecoverAsync(
                new DocumentRefundCorrectionRecoveryRequest(
                    CorrectionKey(operation, refund),
                    order.Id,
                    operation.OperationId,
                    ticket.DocumentNumber,
                    refund.Id),
                cancellationToken);

            if (recovery.Outcome == ProviderOperationOutcome.Rejected)
                return await RejectAsync(order, operation, ticket, refund, cancellationToken);

            if (recovery.Outcome != ProviderOperationOutcome.Confirmed)
                return await ReconcileAsync(order, operation, ticket, refund, recovery.Outcome, cancellationToken);

            StagedRefundCorrection staged;

            try
            {
                ticket.EnsureRefundCanBeCancelled(refund.Id);

                staged = PrepareCorrection(order, operation, ticket, refund, execution);
            }
            catch
            {
                return await ReconcileAsync(
                    order, operation, ticket, refund, ProviderOperationOutcome.Unknown, cancellationToken);
            }

            return await FinalizeAsync(
                order, operation, ticket, refund, execution, staged,
                recovery.ProviderReference, RefundValueDispatch.AfterRecovery, cancellationToken);
        }

        private async Task<CancelRefundOutcome> ReplayFinalizedAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            Entities.OrderChange committed,
            CancellationToken cancellationToken)
        {
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var changeSet = order.PriceChangeSets.FirstOrDefault(set => set.ChangeId == committed.Id);
            var correction = ticket.RefundCorrectionOf(operation.OperationId);

            var corrected = new CorrectedRefund(
                committed.Id,
                changeSet?.Id ?? 0,
                refund.Id,
                correction?.RestoredOrderServiceIds().ToList() ?? [],
                changeSet?.FinancialSequence ?? order.FinancialSequence);

            return Outcome(
                order, operation, ticket, refund, correction, corrected,
                ProviderOperationOutcome.Confirmed,
                correction?.ValueCorrectionStatus ?? ProviderOperationOutcome.Pending,
                ServicingOperationStatus.Completed, correctionNotAvailable: false, isReplay: true);
        }

        private static Entities.OrderChange? CommittedCorrection(Order order, long operationId)
            => order.Changes.FirstOrDefault(change =>
                change.OperationId == operationId && change.ChangeType == OrderChangeType.CancelRefund);

        private async Task<ElectronicTicket> ResolveAsync(
            long orderId,
            long electronicTicketId,
            CancellationToken cancellationToken)
        {
            var tickets = await _tickets.ListByOrderAsync(orderId, cancellationToken);

            return tickets.FirstOrDefault(candidate => candidate.Id == electronicTicketId)
                   ?? throw ExceptionFactory.AccountableDocumentNotFound(electronicTicketId, orderId);
        }

        private string CorrectionKey(OrderOperation operation, DocumentRefundRecord refund)
            => _operations.ProviderOperationKey(operation, $"{CorrectionStep}:{refund.Id}");

        private async Task TryReleaseRejectedAsync(long orderId, OrderOperation operation, CancellationToken cancellationToken)
        {
            try
            {
                await _operations.ResolveAsync(orderId, operation, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception)
            {
            }
        }

        private static CancelRefundOutcome Outcome(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord refund,
            DocumentRefundCorrectionRecord? correction,
            CorrectedRefund? corrected,
            ProviderOperationOutcome documentOutcome,
            ProviderOperationOutcome valueOutcome,
            ServicingOperationStatus operationStatus,
            bool correctionNotAvailable,
            bool isReplay)
            => new(
                order.Id,
                operation.OperationId,
                ticket.Id,
                ticket.DocumentNumber,
                ticket.DocumentVersion,
                ticket.StatusSummary,
                refund.Id,
                refund.OperationId,
                correction?.Id,
                corrected?.OrderChangeId,
                corrected?.PriceChangeSetId,
                correction?.Coupons.Select(coupon => coupon.TicketCouponId).ToList() ?? [],
                corrected?.RestoredOrderServiceIds ?? [],
                correction?.CorrectedAmount ?? 0m,
                order.CommercialVersion,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                documentOutcome,
                valueOutcome,
                operationStatus,
                correctionNotAvailable,
                isReplay);
    }
}
