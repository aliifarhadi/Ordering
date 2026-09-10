using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Arguments;
using AeroTech.Ordering.Domain.Servicing.Operations;
using AeroTech.Ordering.Domain.Servicing.Operations.Contracts;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.ValueObjects;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Ports.DocumentRefund;
using AeroTech.Ordering.Domain.Ports.Refund;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using Entities = AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public sealed class RefundService : IRefundService
    {
        public const string QuoteStep = "accept-quoted-refund";
        public const string DocumentStep = "document-refund";
        public const string ManualSourceSystem = "Manual";

        private readonly IOrderRepository _orders;
        private readonly IElectronicTicketRepository _tickets;
        private readonly IRefundQuotePort _quotes;
        private readonly IDocumentRefundPort _documents;
        private readonly IRefundValueMovementCoordinator _valueMovement;
        private readonly IManualRefundAuthorizer _manualAuthorizer;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IServicingOperationStore _operationStore;
        private readonly ICommandReceiptStore _receipts;
        private readonly ICallerContext _callerContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;

        public RefundService(
            IOrderRepository orders,
            IElectronicTicketRepository tickets,
            IRefundQuotePort quotes,
            IDocumentRefundPort documents,
            IRefundValueMovementCoordinator valueMovement,
            IManualRefundAuthorizer manualAuthorizer,
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
            _quotes = quotes;
            _documents = documents;
            _valueMovement = valueMovement;
            _manualAuthorizer = manualAuthorizer;
            _operations = operations;
            _operationStore = operationStore;
            _receipts = receipts;
            _callerContext = callerContext;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
            _projector = projector;
        }

        public async Task<RefundQuoteOutcome> QuoteAsync(
            long orderId,
            long electronicTicketId,
            IReadOnlyList<long>? ticketCouponIds = null,
            CancellationToken cancellationToken = default)
        {
            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            var ticket = await ResolveAsync(orderId, electronicTicketId, cancellationToken);
            var scope = ResolveScope(ticket, ticketCouponIds);

            ticket.EnsureRefundScopeIsEligible(scope);

            var quote = await _quotes.QuoteAsync(
                new RefundQuoteRequest(
                    order.Id,
                    order.CommercialVersion,
                    ticket.Id,
                    ticket.DocumentNumber,
                    scope,
                    order.CurrencyId),
                cancellationToken);

            RefundQuoteBinding.EnsureQuoteBindsToTheOrder(quote, order, ticket, scope);

            return new RefundQuoteOutcome(
                order.Id,
                order.CommercialVersion,
                ticket.Id,
                ticket.DocumentNumber,
                quote.QuotedRefundId,
                quote.SourceSystem,
                quote.PricingSource,
                quote.ApprovedRefundAmount,
                quote.SaleCurrencyId,
                quote.ApprovedDisposition,
                quote.ExpiresAt,
                quote.TicketCouponIds,
                quote.PricingLines,
                quote.SourcePricingReference,
                quote.SourceRefundType,
                quote.SourceEvidence);
        }

        public async Task<RefundOutcome> RefundAsync(
            RefundExecution execution,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            ArgumentException.ThrowIfNullOrWhiteSpace(execution.IdempotencyKey);

            if (!execution.IsManual && string.IsNullOrWhiteSpace(execution.QuotedRefundId))
                throw ExceptionFactory.OrderRefundRequiresQuote(execution.OrderId);

            if (execution.ExpectedCommercialVersion is null)
                throw ExceptionFactory.ExpectedCommercialVersionRequired(execution.OrderId);

            if (execution.TicketCouponIds.Count == 0)
                throw ExceptionFactory.RefundRequiresCouponScope(execution.OrderId);

            var order = await _orders.GetAsync(execution.OrderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(execution.OrderId);

            var ticket = await ResolveAsync(execution.OrderId, execution.ElectronicTicketId, cancellationToken);
            var scope = execution.TicketCouponIds.Distinct().Order().ToList();

            var authority = execution.Manual is { } manual
                ? ManualRefundPreconditions.EnsureContextIsEligible(_callerContext, manual, execution.OrderId)
                : null;

            var operation = await _operations.BeginAsync(
                execution.OrderId,
                ServicingOperationKind.Refund,
                execution.IdempotencyKey,
                new
                {
                    Operation = "Refund",
                    OrderId = execution.OrderId,
                    ElectronicTicketId = execution.ElectronicTicketId,
                    TicketCouponIds = scope.ToArray(),
                    QuotedRefundId = execution.QuotedRefundId,
                    ManualAuthorityReference = authority?.Reference,
                    ExpectedCommercialVersion = execution.ExpectedCommercialVersion
                },
                execution.ExpectedCommercialVersion,
                cancellationToken);

            var committed = CommittedRefund(order, operation.OperationId);

            if (committed is not null)
                return await ReplayFinalizedAsync(order, operation, ticket, committed, cancellationToken);

            if (operation.IsReplay)
            {
                var unfinished = await ReplayUnfinishedAsync(
                    order, operation, ticket, execution, scope, authority, cancellationToken);

                if (unfinished is not null)
                    return unfinished;
            }

            ProviderOperationOutcome outcome;
            string? providerReference;
            AcceptedRefund accepted;
            StagedRefund staged;

            try
            {
                if (execution.ExpectedCommercialVersion != order.CommercialVersion)
                    throw ExceptionFactory.OrderCommercialVersionMismatch(
                        execution.ExpectedCommercialVersion,
                        execution.OrderId,
                        order.CommercialVersion);

                ticket.EnsureRefundScopeIsEligible(scope);

                if (authority is not null)
                    await _manualAuthorizer.AuthorizeAsync(
                        order,
                        operation,
                        ticket,
                        authority,
                        execution.Manual!.ApprovedRefundAmount,
                        cancellationToken);

                await _operationStore.TransitionAsync(
                    operation.OperationId,
                    ServicingOperationStatus.Executing,
                    operation.ClaimGeneration,
                    cancellationToken);

                var eligibility = await _documents.CheckEligibilityAsync(
                    new DocumentRefundEligibilityRequest(
                        DocumentKey(operation, ticket),
                        execution.OrderId,
                        operation.OperationId,
                        ticket.DocumentNumber,
                        CouponNumbers(ticket, scope)),
                    cancellationToken);

                if (eligibility.Outcome == EligibilityOutcome.Denied)
                    return await RefuseAsync(order, operation, ticket, cancellationToken);

                if (eligibility.Outcome == EligibilityOutcome.PendingEvidence)
                    return await SuspendAsync(order, operation, ticket, ProviderOperationOutcome.Pending, cancellationToken);

                accepted = await AcceptRefundAsync(order, operation, ticket, execution, scope, cancellationToken);
                staged = PrepareRefund(order, operation, ticket, accepted, scope);

                var result = await _documents.RefundAsync(
                    new DocumentRefundRequest(
                        DocumentKey(operation, ticket),
                        execution.OrderId,
                        operation.OperationId,
                        ticket.DocumentNumber,
                        CouponNumbers(ticket, scope)),
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
                    order, operation, ticket, accepted, staged, scope, authority,
                    providerReference, RefundValueDispatch.FirstAttempt, cancellationToken),
                ProviderOperationOutcome.Rejected => await RejectAsync(order, operation, ticket, cancellationToken),
                _ => await SuspendAsync(order, operation, ticket, outcome, cancellationToken)
            };
        }

        private async Task<AcceptedRefund> AcceptRefundAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            RefundExecution execution,
            IReadOnlyList<long> scope,
            CancellationToken cancellationToken)
        {
            var accepted = execution.Manual is { } manual
                ? ManualAccepted(order, ticket, scope, manual)
                : await _quotes.AcceptQuotedRefundAsync(
                    new AcceptedQuotedRefundSelection(
                        _operations.ProviderOperationKey(operation, QuoteStep),
                        order.Id,
                        operation.OperationId,
                        execution.QuotedRefundId!,
                        execution.ExpectedCommercialVersion!.Value,
                        ticket.Id,
                        ticket.DocumentNumber,
                        scope,
                        order.CurrencyId),
                    cancellationToken);

            if (execution.Manual is null)
                RefundQuoteBinding.EnsureQuotedRefundStillStands(
                    accepted, execution.QuotedRefundId!, _clock.GetDateTime());
            else
                RefundPricingAuthorityPolicy.EnsureManualAuthority(accepted.PricingSource);

            RefundQuoteBinding.EnsureAcceptedBindsToTheRequest(
                accepted, order, ticket, scope, execution.ExpectedCommercialVersion!.Value);

            return accepted;
        }

        private AcceptedRefund ManualAccepted(
            Order order,
            ElectronicTicket ticket,
            IReadOnlyList<long> scope,
            ManualRefundInstruction manual)
            => new(
                ManualSourceSystem,
                manual.AuthorityReference,
                PricingSource.Manual,
                order.Id,
                order.CommercialVersion,
                ticket.Id,
                order.CurrencyId,
                scope,
                manual.PricingLines,
                manual.ApprovedRefundAmount,
                manual.ApprovedDisposition,
                _clock.GetDateTime(),
                manual.SourcePricingReference,
                manual.DispositionReference,
                manual.SourceRefundType,
                manual.SourceEvidence);

        private StagedRefund PrepareRefund(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            AcceptedRefund accepted,
            IReadOnlyList<long> scope)
            => order.PrepareRefund(
                new AcceptedRefundArgs(
                    accepted,
                    ticket.ServiceIdsClosedByRefundOf(scope).ToList(),
                    ticket.CarriedPricingLineIds(),
                    operation.OperationId,
                    _callerContext.ActorId,
                    CallerScope.For(_callerContext)),
                _idGenerator,
                _clock);

        private async Task<RefundOutcome> FinalizeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            AcceptedRefund accepted,
            StagedRefund staged,
            IReadOnlyList<long> scope,
            ManualRefundAuthority? authority,
            string? providerReference,
            RefundValueDispatch dispatch,
            CancellationToken cancellationToken)
        {
            var record = ticket.Refund(
                operation.OperationId,
                scope,
                ProvenanceOf(accepted, authority),
                providerReference,
                _callerContext.ActorId,
                CallerScope.For(_callerContext),
                _idGenerator,
                _clock);

            var refunded = order.CommitRefund(staged, _idGenerator, _clock);

            ticket.AttachRefundPriceChangeSet(operation.OperationId, refunded.PriceChangeSetId);

            var valueOutcome = await _valueMovement.SettleAsync(
                order.Id, operation, ticket, accepted, dispatch, cancellationToken);

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
                order, operation, ticket, record, refunded,
                ProviderOperationOutcome.Confirmed, valueOutcome,
                ServicingOperationStatus.Completed, refundNotAvailable: false, isReplay: false);
        }

        private async Task<RefundOutcome> RejectAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            CancellationToken cancellationToken)
            => await SettleUnfinishedAsync(
                order, operation, ticket,
                ServicingOperationStatus.Rejected,
                CommandReceiptStatus.Rejected,
                ProviderOperationOutcome.Rejected,
                releaseClaim: true, refundNotAvailable: false, isReplay: false, cancellationToken);

        private async Task<RefundOutcome> RefuseAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            CancellationToken cancellationToken)
            => await SettleUnfinishedAsync(
                order, operation, ticket,
                ServicingOperationStatus.Rejected,
                CommandReceiptStatus.Rejected,
                ProviderOperationOutcome.Rejected,
                releaseClaim: true, refundNotAvailable: true, isReplay: false, cancellationToken);

        private async Task<RefundOutcome> SuspendAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            ProviderOperationOutcome outcome,
            CancellationToken cancellationToken)
            => await SettleUnfinishedAsync(
                order, operation, ticket,
                ServicingOperationStatus.AwaitingExternal,
                outcome == ProviderOperationOutcome.Unknown
                    ? CommandReceiptStatus.Unknown
                    : CommandReceiptStatus.Pending,
                outcome,
                releaseClaim: false, refundNotAvailable: false, isReplay: false, cancellationToken);

        private async Task<RefundOutcome> ReconcileAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            ProviderOperationOutcome outcome,
            CancellationToken cancellationToken)
            => await SettleUnfinishedAsync(
                order, operation, ticket,
                ServicingOperationStatus.NeedsReconciliation,
                CommandReceiptStatus.NeedsReconciliation,
                outcome,
                releaseClaim: false, refundNotAvailable: false, isReplay: true, cancellationToken);

        private async Task<RefundOutcome> SettleUnfinishedAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            ServicingOperationStatus operationStatus,
            CommandReceiptStatus receiptStatus,
            ProviderOperationOutcome documentOutcome,
            bool releaseClaim,
            bool refundNotAvailable,
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
                order, operation, ticket, null, null,
                documentOutcome, ProviderOperationOutcome.Pending,
                operationStatus, refundNotAvailable, isReplay);
        }

        private async Task<RefundOutcome?> ReplayUnfinishedAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            RefundExecution execution,
            IReadOnlyList<long> scope,
            ManualRefundAuthority? authority,
            CancellationToken cancellationToken)
        {
            var prior = await _operationStore.FindAsync(operation.OperationId, cancellationToken);

            if (prior is null)
                return null;

            if (prior.Status == ServicingOperationStatus.Rejected)
                return Outcome(
                    order, operation, ticket, null, null,
                    ProviderOperationOutcome.Rejected, ProviderOperationOutcome.Pending,
                    prior.Status, refundNotAvailable: false, isReplay: true);

            if (prior.Status is not (ServicingOperationStatus.Executing
                or ServicingOperationStatus.AwaitingExternal
                or ServicingOperationStatus.NeedsReconciliation))
                return null;

            var recovery = await _documents.RecoverAsync(
                new DocumentRefundRecoveryRequest(
                    DocumentKey(operation, ticket),
                    order.Id,
                    operation.OperationId,
                    ticket.DocumentNumber),
                cancellationToken);

            if (recovery.Outcome == ProviderOperationOutcome.Rejected)
                return await RejectAsync(order, operation, ticket, cancellationToken);

            if (recovery.Outcome != ProviderOperationOutcome.Confirmed)
                return await ReconcileAsync(order, operation, ticket, recovery.Outcome, cancellationToken);

            AcceptedRefund accepted;
            StagedRefund staged;

            try
            {
                accepted = await AcceptRefundAsync(order, operation, ticket, execution, scope, cancellationToken);
                staged = PrepareRefund(order, operation, ticket, accepted, scope);
            }
            catch
            {
                return await ReconcileAsync(order, operation, ticket, ProviderOperationOutcome.Unknown, cancellationToken);
            }

            return await FinalizeAsync(
                order, operation, ticket, accepted, staged, scope, authority,
                recovery.ProviderReference, RefundValueDispatch.AfterRecovery, cancellationToken);
        }

        private async Task<RefundOutcome> ReplayFinalizedAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            Entities.OrderChange committed,
            CancellationToken cancellationToken)
        {
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var changeSet = order.PriceChangeSets.FirstOrDefault(set => set.ChangeId == committed.Id);
            var record = ticket.RefundOf(operation.OperationId);

            var refunded = new RefundedDocument(
                committed.Id,
                changeSet?.Id ?? 0,
                record?.RefundedOrderServiceIds().ToList() ?? [],
                changeSet?.FinancialSequence ?? order.FinancialSequence);

            return Outcome(
                order, operation, ticket, record, refunded,
                ProviderOperationOutcome.Confirmed,
                record?.ValueMovementStatus ?? ProviderOperationOutcome.Pending,
                ServicingOperationStatus.Completed, refundNotAvailable: false, isReplay: true);
        }

        private static Entities.OrderChange? CommittedRefund(Order order, long operationId)
            => order.Changes.FirstOrDefault(change =>
                change.OperationId == operationId && change.ChangeType == OrderChangeType.Refund);

        private async Task<ElectronicTicket> ResolveAsync(
            long orderId,
            long electronicTicketId,
            CancellationToken cancellationToken)
        {
            var tickets = await _tickets.ListByOrderAsync(orderId, cancellationToken);

            return tickets.FirstOrDefault(candidate => candidate.Id == electronicTicketId)
                   ?? throw ExceptionFactory.AccountableDocumentNotFound(electronicTicketId, orderId);
        }

        private static IReadOnlyList<long> ResolveScope(ElectronicTicket ticket, IReadOnlyList<long>? requested)
            => requested is { Count: > 0 }
                ? requested.Distinct().Order().ToList()
                : ticket.RefundableCouponIds().ToList();

        private static IReadOnlyList<int> CouponNumbers(ElectronicTicket ticket, IReadOnlyCollection<long> scope)
            => ticket.Coupons
                .Where(coupon => scope.Contains(coupon.Id))
                .Select(coupon => coupon.CouponNumber)
                .Order()
                .ToList();

        private string DocumentKey(OrderOperation operation, ElectronicTicket ticket)
            => _operations.ProviderOperationKey(operation, $"{DocumentStep}:{ticket.Id}");

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

        private static RefundOutcome Outcome(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            DocumentRefundRecord? record,
            RefundedDocument? refunded,
            ProviderOperationOutcome documentOutcome,
            ProviderOperationOutcome valueOutcome,
            ServicingOperationStatus operationStatus,
            bool refundNotAvailable,
            bool isReplay)
            => new(
                order.Id,
                operation.OperationId,
                ticket.Id,
                ticket.DocumentNumber,
                ticket.DocumentVersion,
                ticket.StatusSummary,
                record?.Id,
                refunded?.OrderChangeId,
                refunded?.PriceChangeSetId,
                record?.Coupons.Select(coupon => coupon.TicketCouponId).ToList() ?? [],
                refunded?.RefundedServiceIds ?? [],
                record?.PricingSource ?? PricingSource.PricingEngine,
                record?.ApprovedAmount ?? 0m,
                order.CommercialVersion,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                documentOutcome,
                valueOutcome,
                operationStatus,
                refundNotAvailable,
                isReplay);
        private static RefundProvenance ProvenanceOf(AcceptedRefund accepted, ManualRefundAuthority? authority)
            => new(
                accepted.QuotedRefundId,
                accepted.PricingSource,
                accepted.ApprovedRefundAmount,
                accepted.ApprovedDisposition,
                accepted.DispositionReference,
                accepted.SourcePricingReference,
                accepted.SourceRefundType,
                accepted.SourceEvidence,
                authority);
    }
}
