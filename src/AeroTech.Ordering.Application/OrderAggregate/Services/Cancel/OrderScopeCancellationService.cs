using AeroTech.Ordering.Domain.Servicing.Operations;
using AeroTech.Ordering.Domain.Servicing.Operations.Contracts;
using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.ScopeCancellation;
using AeroTech.Ordering.Domain.OrderAggregate.Arguments;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.Ports.OrderChange;
using AeroTech.Ordering.Domain._Shared.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using Entities = AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Cancel
{
    public sealed class OrderScopeCancellationService : IOrderScopeCancellationService
    {
        public const string QuoteStep = "accept-quoted-cancellation";

        private readonly IOrderRepository _orders;
        private readonly IElectronicTicketRepository _tickets;
        private readonly IElectronicMiscDocumentRepository _miscDocuments;
        private readonly IOrderCancellationQuoteProvider _quotes;
        private readonly IReservationReleaseCoordinator _release;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IServicingOperationStore _operationStore;
        private readonly ICommandReceiptStore _receipts;
        private readonly ICallerContext _callerContext;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;

        public OrderScopeCancellationService(
            IOrderRepository orders,
            IElectronicTicketRepository tickets,
            IElectronicMiscDocumentRepository miscDocuments,
            IOrderCancellationQuoteProvider quotes,
            IReservationReleaseCoordinator release,
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
            _miscDocuments = miscDocuments;
            _quotes = quotes;
            _release = release;
            _operations = operations;
            _operationStore = operationStore;
            _receipts = receipts;
            _callerContext = callerContext;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
            _projector = projector;
        }

        public Task<ScopeCancellationOutcome> CancelItemAsync(
            long orderId,
            long orderItemId,
            string quotedCancellationId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(
                orderId,
                OrderChangeType.Cancel,
                ServicingOperationKind.Cancel,
                orderItemId,
                null,
                quotedCancellationId,
                idempotencyKey,
                expectedCommercialVersion,
                cancellationToken);

        public Task<ScopeCancellationOutcome> RemoveServicesAsync(
            long orderId,
            IReadOnlyList<long> orderServiceIds,
            string quotedCancellationId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(
                orderId,
                OrderChangeType.RemoveService,
                ServicingOperationKind.RemoveService,
                null,
                orderServiceIds,
                quotedCancellationId,
                idempotencyKey,
                expectedCommercialVersion,
                cancellationToken);

        private async Task<ScopeCancellationOutcome> ExecuteAsync(
            long orderId,
            OrderChangeType intent,
            ServicingOperationKind kind,
            long? orderItemId,
            IReadOnlyList<long>? requestedServiceIds,
            string quotedCancellationId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

            if (string.IsNullOrWhiteSpace(quotedCancellationId))
                throw ExceptionFactory.OrderScopeCancellationRequiresQuote(orderId);

            if (expectedCommercialVersion is null)
                throw ExceptionFactory.ExpectedCommercialVersionRequired(orderId);

            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            var requested = (requestedServiceIds ?? []).Distinct().Order().ToArray();

            var operation = await _operations.BeginAsync(
                orderId,
                kind,
                idempotencyKey,
                new
                {
                    Operation = intent.ToString(),
                    OrderId = orderId,
                    OrderItemId = orderItemId,
                    OrderServiceIds = requested,
                    QuotedCancellationId = quotedCancellationId,
                    ExpectedCommercialVersion = expectedCommercialVersion
                },
                expectedCommercialVersion,
                cancellationToken);

            var scope = intent == OrderChangeType.Cancel
                ? order.ServiceIdsOfItem(orderItemId!.Value).ToList()
                : requested.ToList();

            var committed = CommittedScope(order, operation.OperationId, intent);

            if (committed is not null)
                return await ReplayFinalizedAsync(order, operation, committed, cancellationToken);

            if (operation.IsReplay)
            {
                var unfinished = await ReplayUnfinishedAsync(order, operation, intent, scope, cancellationToken);

                if (unfinished is not null)
                    return unfinished;
            }

            ProviderOperationOutcome releaseOutcome;
            AcceptedScopeCancellation accepted;

            try
            {
                if (expectedCommercialVersion != order.CommercialVersion)
                    throw ExceptionFactory.OrderCommercialVersionMismatch(
                        expectedCommercialVersion,
                        orderId,
                        order.CommercialVersion);

                order.EnsureScopeCanBeCancelled(intent, orderItemId, scope);
                await EnsureScopeIsNotDocumentedAsync(orderId, scope, kind, cancellationToken);

                await _operationStore.TransitionAsync(
                    operation.OperationId,
                    ServicingOperationStatus.Executing,
                    operation.ClaimGeneration,
                    cancellationToken);

                accepted = await _quotes.AcceptQuotedCancellationAsync(
                    new AcceptedQuotedCancellationSelection(
                        _operations.ProviderOperationKey(operation, QuoteStep),
                        orderId,
                        operation.OperationId,
                        quotedCancellationId,
                        intent,
                        expectedCommercialVersion.Value,
                        orderItemId,
                        scope,
                        order.CurrencyId),
                    cancellationToken);

                EnsureAcceptedBindsToTheRequest(
                    accepted,
                    order,
                    intent,
                    scope,
                    quotedCancellationId,
                    expectedCommercialVersion.Value);

                releaseOutcome = await _release.ReleaseAsync(orderId, operation, scope, cancellationToken);
            }
            catch
            {
                await TryReleaseRejectedAsync(orderId, operation, cancellationToken);
                throw;
            }

            if (releaseOutcome == ProviderOperationOutcome.Rejected)
                return await RejectAsync(order, operation, intent, cancellationToken);

            if (releaseOutcome != ProviderOperationOutcome.Confirmed)
                return await SuspendAsync(order, operation, intent, releaseOutcome, cancellationToken);

            return await FinalizeAsync(order, operation, intent, orderItemId, accepted, cancellationToken);
        }

        private void EnsureAcceptedBindsToTheRequest(
            AcceptedScopeCancellation accepted,
            Order order,
            OrderChangeType intent,
            IReadOnlyCollection<long> scope,
            string quotedCancellationId,
            int expectedCommercialVersion)
        {
            if (!string.Equals(accepted.QuotedCancellationId, quotedCancellationId, StringComparison.Ordinal))
                throw ExceptionFactory.AcceptedCancellationDoesNotMatchTheRequest("quoted cancellation identity");

            if (accepted.OrderId != order.Id)
                throw ExceptionFactory.AcceptedCancellationDoesNotMatchTheRequest("order");

            if (accepted.ExpectedCommercialVersion != expectedCommercialVersion)
                throw ExceptionFactory.AcceptedCancellationDoesNotMatchTheRequest("commercial version");

            if (accepted.Intent != intent)
                throw ExceptionFactory.AcceptedCancellationDoesNotMatchTheRequest("operation intent");

            if (accepted.SaleCurrencyId != order.CurrencyId)
                throw ExceptionFactory.AcceptedCancellationDoesNotMatchTheRequest("sale currency");

            if (!accepted.CancelledOrderServiceIds.Distinct().ToHashSet().SetEquals(scope))
                throw ExceptionFactory.AcceptedCancellationScopeMismatch();
        }

        private async Task<ScopeCancellationOutcome> FinalizeAsync(
            Order order,
            OrderOperation operation,
            OrderChangeType intent,
            long? orderItemId,
            AcceptedScopeCancellation accepted,
            CancellationToken cancellationToken)
        {
            CancelledScope cancelled;

            try
            {
                cancelled = order.CancelScope(
                    new AcceptedScopeCancellationArgs(
                        accepted,
                        intent,
                        operation.OperationId,
                        orderItemId,
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
                intent,
                cancelled,
                ProviderOperationOutcome.Confirmed,
                ServicingOperationStatus.Completed,
                isReplay: false);
        }

        private async Task<ScopeCancellationOutcome> RejectAsync(
            Order order,
            OrderOperation operation,
            OrderChangeType intent,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.Rejected,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.Rejected, cancellationToken);
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(order, operation, intent, null, ProviderOperationOutcome.Rejected, ServicingOperationStatus.Rejected, false);
        }

        private async Task<ScopeCancellationOutcome> SuspendAsync(
            Order order,
            OrderOperation operation,
            OrderChangeType intent,
            ProviderOperationOutcome outcome,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.AwaitingExternal,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(
                operation.ReceiptId,
                outcome == ProviderOperationOutcome.Unknown
                    ? CommandReceiptStatus.Unknown
                    : CommandReceiptStatus.Pending,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(order, operation, intent, null, outcome, ServicingOperationStatus.AwaitingExternal, false);
        }

        private async Task<ScopeCancellationOutcome> ReconcileAsync(
            Order order,
            OrderOperation operation,
            OrderChangeType intent,
            ProviderOperationOutcome outcome,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.NeedsReconciliation,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(operation.ReceiptId, CommandReceiptStatus.NeedsReconciliation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(order, operation, intent, null, outcome, ServicingOperationStatus.NeedsReconciliation, true);
        }

        private async Task<ScopeCancellationOutcome?> ReplayUnfinishedAsync(
            Order order,
            OrderOperation operation,
            OrderChangeType intent,
            IReadOnlyList<long> scope,
            CancellationToken cancellationToken)
        {
            var prior = await _operationStore.FindAsync(operation.OperationId, cancellationToken);

            if (prior is null)
                return null;

            if (prior.Status == ServicingOperationStatus.Rejected)
                return Outcome(order, operation, intent, null, ProviderOperationOutcome.Rejected, prior.Status, true);

            if (prior.Status is not (ServicingOperationStatus.Executing
                or ServicingOperationStatus.AwaitingExternal
                or ServicingOperationStatus.NeedsReconciliation))
                return null;

            var recovered = await _release.RecoverAsync(order.Id, operation, scope, cancellationToken);

            if (recovered == ProviderOperationOutcome.Rejected)
                return await RejectAsync(order, operation, intent, cancellationToken);

            if (recovered != ProviderOperationOutcome.Confirmed)
                return await ReconcileAsync(order, operation, intent, recovered, cancellationToken);

            if (await _release.HasOutstandingObligationAsync(order.Id, scope, cancellationToken))
                return await ReconcileAsync(order, operation, intent, ProviderOperationOutcome.Unknown, cancellationToken);

            return null;
        }

        private async Task EnsureScopeIsNotDocumentedAsync(
            long orderId,
            IReadOnlyCollection<long> scope,
            ServicingOperationKind kind,
            CancellationToken cancellationToken)
        {
            var tickets = await _tickets.ListByOrderAsync(orderId, cancellationToken);
            var miscDocuments = await _miscDocuments.ListByOrderAsync(orderId, cancellationToken);

            var documented = tickets
                .SelectMany(ticket => ticket.Coupons)
                .Where(coupon => coupon.FinancialStatus != TicketCouponFinancialStatus.Void)
                .Select(coupon => coupon.CurrentOrderServiceId)
                .ToList();

            documented.AddRange(miscDocuments
                .SelectMany(document => document.Coupons)
                .Where(coupon => coupon.OrderServiceId is not null)
                .Select(coupon => coupon.OrderServiceId!.Value));

            if (documented.Any(scope.Contains))
                throw ExceptionFactory.OrderOperationNotEligible(
                    kind,
                    orderId,
                    Domain.OrderAggregate.Policies.EligibilityReasonCodes.AlreadyIssued);
        }

        private static Entities.OrderChange? CommittedScope(Order order, long operationId, OrderChangeType intent)
            => order.Changes.FirstOrDefault(change =>
                change.OperationId == operationId && change.ChangeType == intent);

        private async Task<ScopeCancellationOutcome> ReplayFinalizedAsync(
            Order order,
            OrderOperation operation,
            Entities.OrderChange committed,
            CancellationToken cancellationToken)
        {
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var changeSet = order.PriceChangeSets.FirstOrDefault(set => set.ChangeId == committed.Id);

            var cancelled = new CancelledScope(
                committed.Id,
                committed.ChangeType,
                changeSet?.Id,
                [],
                [],
                changeSet?.FinancialSequence ?? order.FinancialSequence);

            return Outcome(
                order,
                operation,
                committed.ChangeType,
                cancelled,
                ProviderOperationOutcome.Confirmed,
                ServicingOperationStatus.Completed,
                isReplay: true);
        }

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

        private static ScopeCancellationOutcome Outcome(
            Order order,
            OrderOperation operation,
            OrderChangeType intent,
            CancelledScope? cancelled,
            ProviderOperationOutcome releaseOutcome,
            ServicingOperationStatus operationStatus,
            bool isReplay)
            => new(
                order.Id,
                operation.OperationId,
                intent,
                cancelled?.OrderChangeId ?? 0,
                cancelled?.PriceChangeSetId,
                cancelled?.CancelledServiceIds ?? [],
                cancelled?.CancelledItemIds ?? [],
                order.CommercialVersion,
                order.FinancialSequence,
                order.ObligationVersion,
                order.CustomerTotal,
                releaseOutcome,
                operationStatus,
                isReplay);
    }
}
