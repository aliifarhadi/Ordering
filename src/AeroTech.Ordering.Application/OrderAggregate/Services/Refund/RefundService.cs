using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Dto;
using AeroTech.Ordering.Domain.Ports.DocumentRefund;
using AeroTech.Ordering.Domain.Ports.Refund;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Operations;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using Entities = AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Refund
{
    public sealed class RefundService : IRefundService
    {
        public const string QuoteStep = "accept-quoted-refund";
        public const string DocumentStep = "document-refund";

        private readonly IOrderRepository _orders;
        private readonly IElectronicTicketRepository _tickets;
        private readonly IRefundQuotePort _quotes;
        private readonly IDocumentRefundPort _documents;
        private readonly IRefundValueMovementCoordinator _valueMovement;
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
            CancellationToken cancellationToken = default)
        {
            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            var ticket = await ResolveAsync(orderId, electronicTicketId, cancellationToken);
            var couponIds = CouponIds(ticket);

            ticket.EnsureFullUnusedRefundIsSupported(couponIds);

            var quote = await _quotes.QuoteAsync(
                new RefundQuoteRequest(
                    order.Id,
                    order.CommercialVersion,
                    ticket.Id,
                    ticket.DocumentNumber,
                    couponIds,
                    order.CurrencyId),
                cancellationToken);

            RefundQuoteBinding.EnsureQuoteBindsToTheOrder(quote, order, ticket, couponIds);

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
                quote.SourcePricingReference);
        }

        public async Task<RefundOutcome> RefundAsync(
            long orderId,
            long electronicTicketId,
            string quotedRefundId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

            if (string.IsNullOrWhiteSpace(quotedRefundId))
                throw ExceptionFactory.OrderRefundRequiresQuote(orderId);

            if (expectedCommercialVersion is null)
                throw ExceptionFactory.ExpectedCommercialVersionRequired(orderId);

            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            var ticket = await ResolveAsync(orderId, electronicTicketId, cancellationToken);

            var operation = await _operations.BeginAsync(
                orderId,
                ServicingOperationKind.Refund,
                idempotencyKey,
                new
                {
                    Operation = "Refund",
                    OrderId = orderId,
                    ElectronicTicketId = electronicTicketId,
                    QuotedRefundId = quotedRefundId,
                    ExpectedCommercialVersion = expectedCommercialVersion
                },
                expectedCommercialVersion,
                cancellationToken);

            var committed = CommittedRefund(order, operation.OperationId);

            if (committed is not null)
                return await ReplayFinalizedAsync(order, operation, ticket, committed, cancellationToken);

            if (operation.IsReplay)
            {
                var unfinished = await ReplayUnfinishedAsync(
                    order,
                    operation,
                    ticket,
                    quotedRefundId,
                    expectedCommercialVersion.Value,
                    cancellationToken);

                if (unfinished is not null)
                    return unfinished;
            }

            var couponIds = CouponIds(ticket);

            ProviderOperationOutcome outcome;
            string? providerReference;
            AcceptedRefund accepted;

            try
            {
                if (expectedCommercialVersion != order.CommercialVersion)
                    throw ExceptionFactory.OrderCommercialVersionMismatch(
                        expectedCommercialVersion,
                        orderId,
                        order.CommercialVersion);

                ticket.EnsureFullUnusedRefundIsSupported(couponIds);

                await _operationStore.TransitionAsync(
                    operation.OperationId,
                    ServicingOperationStatus.Executing,
                    operation.ClaimGeneration,
                    cancellationToken);

                var eligibility = await _documents.CheckEligibilityAsync(
                    new DocumentRefundEligibilityRequest(
                        DocumentKey(operation, ticket),
                        orderId,
                        operation.OperationId,
                        ticket.DocumentNumber,
                        CouponNumbers(ticket)),
                    cancellationToken);

                if (eligibility.Outcome == EligibilityOutcome.Denied)
                    return await RefuseAsync(order, operation, ticket, cancellationToken);

                if (eligibility.Outcome == EligibilityOutcome.PendingEvidence)
                    return await SuspendAsync(order, operation, ticket, ProviderOperationOutcome.Pending, cancellationToken);

                accepted = await AcceptQuotedRefundAsync(
                    order,
                    operation,
                    ticket,
                    couponIds,
                    quotedRefundId,
                    expectedCommercialVersion.Value,
                    cancellationToken);

                var result = await _documents.RefundAsync(
                    new DocumentRefundRequest(
                        DocumentKey(operation, ticket),
                        orderId,
                        operation.OperationId,
                        ticket.DocumentNumber,
                        CouponNumbers(ticket)),
                    cancellationToken);

                outcome = result.Outcome;
                providerReference = result.ProviderReference;
            }
            catch
            {
                await TryReleaseRejectedAsync(orderId, operation, cancellationToken);
                throw;
            }

            return outcome switch
            {
                ProviderOperationOutcome.Confirmed => await FinalizeAsync(
                    order,
                    operation,
                    ticket,
                    accepted,
                    couponIds,
                    providerReference,
                    cancellationToken),
                ProviderOperationOutcome.Rejected => await RejectAsync(order, operation, ticket, cancellationToken),
                _ => await SuspendAsync(order, operation, ticket, outcome, cancellationToken)
            };
        }

        private async Task<AcceptedRefund> AcceptQuotedRefundAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            IReadOnlyList<long> couponIds,
            string quotedRefundId,
            int expectedCommercialVersion,
            CancellationToken cancellationToken)
        {
            var accepted = await _quotes.AcceptQuotedRefundAsync(
                new AcceptedQuotedRefundSelection(
                    _operations.ProviderOperationKey(operation, QuoteStep),
                    order.Id,
                    operation.OperationId,
                    quotedRefundId,
                    expectedCommercialVersion,
                    ticket.Id,
                    ticket.DocumentNumber,
                    couponIds,
                    order.CurrencyId),
                cancellationToken);

            RefundQuoteBinding.EnsureAcceptedBindsToTheRequest(
                accepted,
                order,
                ticket,
                couponIds,
                quotedRefundId,
                expectedCommercialVersion,
                _clock.GetDateTime());

            return accepted;
        }

        private async Task<RefundOutcome> FinalizeAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            AcceptedRefund accepted,
            IReadOnlyCollection<long> couponIds,
            string? providerReference,
            CancellationToken cancellationToken)
        {
            RefundedDocument refunded;
            DocumentRefundRecord record;

            try
            {
                record = ticket.Refund(
                    operation.OperationId,
                    couponIds,
                    accepted.QuotedRefundId,
                    accepted.SourcePricingReference,
                    accepted.ApprovedRefundAmount,
                    accepted.ApprovedDisposition,
                    accepted.DispositionReference,
                    providerReference,
                    _callerContext.ActorId,
                    CallerScope.For(_callerContext),
                    _idGenerator,
                    _clock);

                refunded = order.CommitRefund(
                    new AcceptedRefundArgs(
                        accepted,
                        record.RefundedOrderServiceIds().ToList(),
                        operation.OperationId,
                        _callerContext.ActorId,
                        CallerScope.For(_callerContext)),
                    _idGenerator,
                    _clock);
            }
            catch
            {
                await TryReleaseRejectedAsync(order.Id, operation, cancellationToken);
                throw;
            }

            ticket.AttachRefundPriceChangeSet(operation.OperationId, refunded.PriceChangeSetId);

            var valueOutcome = await _valueMovement.RequestAsync(
                order.Id,
                operation,
                ticket,
                accepted,
                cancellationToken);

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
                order,
                operation,
                ticket,
                record,
                refunded,
                ProviderOperationOutcome.Confirmed,
                valueOutcome,
                ServicingOperationStatus.Completed,
                refundNotAvailable: false,
                isReplay: false);
        }

        private async Task<RefundOutcome> RejectAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            CancellationToken cancellationToken)
            => await SettleUnfinishedAsync(
                order,
                operation,
                ticket,
                ServicingOperationStatus.Rejected,
                CommandReceiptStatus.Rejected,
                ProviderOperationOutcome.Rejected,
                releaseClaim: true,
                refundNotAvailable: false,
                isReplay: false,
                cancellationToken);

        private async Task<RefundOutcome> RefuseAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            CancellationToken cancellationToken)
            => await SettleUnfinishedAsync(
                order,
                operation,
                ticket,
                ServicingOperationStatus.Rejected,
                CommandReceiptStatus.Rejected,
                ProviderOperationOutcome.Rejected,
                releaseClaim: true,
                refundNotAvailable: true,
                isReplay: false,
                cancellationToken);

        private async Task<RefundOutcome> SuspendAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            ProviderOperationOutcome outcome,
            CancellationToken cancellationToken)
            => await SettleUnfinishedAsync(
                order,
                operation,
                ticket,
                ServicingOperationStatus.AwaitingExternal,
                outcome == ProviderOperationOutcome.Unknown
                    ? CommandReceiptStatus.Unknown
                    : CommandReceiptStatus.Pending,
                outcome,
                releaseClaim: false,
                refundNotAvailable: false,
                isReplay: false,
                cancellationToken);

        private async Task<RefundOutcome> ReconcileAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            ProviderOperationOutcome outcome,
            CancellationToken cancellationToken)
            => await SettleUnfinishedAsync(
                order,
                operation,
                ticket,
                ServicingOperationStatus.NeedsReconciliation,
                CommandReceiptStatus.NeedsReconciliation,
                outcome,
                releaseClaim: false,
                refundNotAvailable: false,
                isReplay: true,
                cancellationToken);

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
                order,
                operation,
                ticket,
                null,
                null,
                documentOutcome,
                ProviderOperationOutcome.Pending,
                operationStatus,
                refundNotAvailable,
                isReplay);
        }

        private async Task<RefundOutcome?> ReplayUnfinishedAsync(
            Order order,
            OrderOperation operation,
            ElectronicTicket ticket,
            string quotedRefundId,
            int expectedCommercialVersion,
            CancellationToken cancellationToken)
        {
            var prior = await _operationStore.FindAsync(operation.OperationId, cancellationToken);

            if (prior is null)
                return null;

            if (prior.Status == ServicingOperationStatus.Rejected)
                return Outcome(
                    order,
                    operation,
                    ticket,
                    null,
                    null,
                    ProviderOperationOutcome.Rejected,
                    ProviderOperationOutcome.Pending,
                    prior.Status,
                    refundNotAvailable: false,
                    isReplay: true);

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

            var couponIds = CouponIds(ticket);

            AcceptedRefund accepted;

            try
            {
                accepted = await AcceptQuotedRefundAsync(
                    order,
                    operation,
                    ticket,
                    couponIds,
                    quotedRefundId,
                    expectedCommercialVersion,
                    cancellationToken);
            }
            catch
            {
                return await ReconcileAsync(order, operation, ticket, ProviderOperationOutcome.Unknown, cancellationToken);
            }

            return await FinalizeAsync(
                order,
                operation,
                ticket,
                accepted,
                couponIds,
                recovery.ProviderReference,
                cancellationToken);
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
                order,
                operation,
                ticket,
                record,
                refunded,
                ProviderOperationOutcome.Confirmed,
                record?.ValueMovementStatus ?? ProviderOperationOutcome.Pending,
                ServicingOperationStatus.Completed,
                refundNotAvailable: false,
                isReplay: true);
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

        private static IReadOnlyList<long> CouponIds(ElectronicTicket ticket)
            => ticket.Coupons.Select(coupon => coupon.Id).Order().ToList();

        private static IReadOnlyList<int> CouponNumbers(ElectronicTicket ticket)
            => ticket.Coupons.Select(coupon => coupon.CouponNumber).Order().ToList();

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
                record?.Id,
                refunded?.OrderChangeId,
                refunded?.PriceChangeSetId,
                refunded?.RefundedServiceIds ?? [],
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
    }
}
