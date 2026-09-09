using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Ports.Reservation;
using AeroTech.Ordering.Domain._Shared.Operations.Contracts;
using AeroTech.Ordering.Domain._Shared.Resources;
using Entities = AeroTech.Ordering.Domain.OrderAggregate.Entities;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Cancel
{
    public sealed class OrderCancelService : IOrderCancelService
    {
        public const string ReleaseStep = "release";

        private readonly IOrderRepository _orders;
        private readonly IElectronicTicketRepository _tickets;
        private readonly IElectronicMiscDocumentRepository _miscDocuments;
        private readonly IFulfillmentReservationRepository _reservations;
        private readonly IReservationPort _reservationPort;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IServicingOperationStore _operationStore;
        private readonly ICommandReceiptStore _receipts;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;

        public OrderCancelService(
            IOrderRepository orders,
            IElectronicTicketRepository tickets,
            IElectronicMiscDocumentRepository miscDocuments,
            IFulfillmentReservationRepository reservations,
            IReservationPort reservationPort,
            IOrderOperationCoordinator operations,
            IServicingOperationStore operationStore,
            ICommandReceiptStore receipts,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock,
            IOrderProjector projector)
        {
            _orders = orders;
            _tickets = tickets;
            _miscDocuments = miscDocuments;
            _reservations = reservations;
            _reservationPort = reservationPort;
            _operations = operations;
            _operationStore = operationStore;
            _receipts = receipts;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
            _projector = projector;
        }

        public async Task<CancelOrderOutcome> CancelAsync(
            long orderId,
            VoidReason reason,
            long cancelledBy,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            var operation = await _operations.BeginAsync(
                orderId,
                ServicingOperationKind.Cancel,
                idempotencyKey,
                new
                {
                    Operation = "Cancel",
                    Scope = "WholeOrder",
                    OrderId = orderId,
                    Reason = reason.ToString()
                },
                expectedCommercialVersion,
                cancellationToken);

            if (CommittedCancel(order, operation.OperationId) is not null)
                return await ReplayFinalizedAsync(order, operation, cancellationToken);

            if (operation.IsReplay)
            {
                var unfinished = await ReplayUnfinishedAsync(order, operation, reason, cancelledBy, cancellationToken);

                if (unfinished is not null)
                    return unfinished;
            }

            IReadOnlyList<long> scope;
            ProviderOperationOutcome releaseOutcome;

            try
            {
                if (expectedCommercialVersion is { } expected && expected != order.CommercialVersion)
                    throw ExceptionFactory.OrderCommercialVersionMismatch(expected, orderId, order.CommercialVersion);

                order.EnsureCanBeCancelled();

                var decision = WithdrawEligibilityPolicy.Evaluate(
                    order,
                    await DocumentedServiceIdsAsync(orderId, cancellationToken));

                if (!decision.IsAllowed)
                    throw ExceptionFactory.OrderOperationNotEligible(
                        ServicingOperationKind.Cancel,
                        orderId,
                        decision.Reasons);

                scope = decision.EffectiveScopeServiceIds;

                await _operationStore.TransitionAsync(
                    operation.OperationId,
                    ServicingOperationStatus.Executing,
                    operation.ClaimGeneration,
                    cancellationToken);

                releaseOutcome = await ReleaseReservationsAsync(orderId, operation, cancellationToken);
            }
            catch
            {
                await TryReleaseRejectedAsync(orderId, operation, cancellationToken);
                throw;
            }

            return releaseOutcome switch
            {
                ProviderOperationOutcome.Confirmed =>
                    await FinalizeAsync(order, operation, scope, reason, cancelledBy, cancellationToken),
                ProviderOperationOutcome.Rejected =>
                    await RejectAsync(order, operation, cancellationToken),
                _ =>
                    await SuspendAsync(order, operation, releaseOutcome, cancellationToken)
            };
        }

        private async Task<CancelOrderOutcome> FinalizeAsync(
            Order order,
            OrderOperation operation,
            IReadOnlyList<long> scope,
            VoidReason reason,
            long cancelledBy,
            CancellationToken cancellationToken)
        {
            order.Cancel(reason, cancelledBy, _clock.GetDateTime(), _idGenerator, operation.OperationId);
            order.ApplyReservationReleased(scope, _clock);

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
                scope,
                ProviderOperationOutcome.Confirmed,
                ServicingOperationStatus.Completed,
                isReplay: false);
        }

        private async Task<CancelOrderOutcome> RejectAsync(
            Order order,
            OrderOperation operation,
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

            return Outcome(
                order,
                operation,
                [],
                ProviderOperationOutcome.Rejected,
                ServicingOperationStatus.Rejected,
                isReplay: false);
        }

        private async Task<CancelOrderOutcome> SuspendAsync(
            Order order,
            OrderOperation operation,
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

            return Outcome(
                order,
                operation,
                [],
                outcome,
                ServicingOperationStatus.AwaitingExternal,
                isReplay: false);
        }

        private async Task<CancelOrderOutcome?> ReplayUnfinishedAsync(
            Order order,
            OrderOperation operation,
            VoidReason reason,
            long cancelledBy,
            CancellationToken cancellationToken)
        {
            var prior = await _operationStore.FindAsync(operation.OperationId, cancellationToken);

            if (prior is null)
                return null;

            if (prior.Status == ServicingOperationStatus.Rejected)
                return Outcome(
                    order,
                    operation,
                    [],
                    ProviderOperationOutcome.Rejected,
                    prior.Status,
                    isReplay: true);

            if (prior.Status is not (ServicingOperationStatus.Executing
                or ServicingOperationStatus.AwaitingExternal
                or ServicingOperationStatus.NeedsReconciliation))
                return null;

            var recovery = await _reservationPort.RecoverAsync(
                new RecoverReservationRequest(
                    _operations.ProviderOperationKey(operation, ReleaseStep),
                    order.Id,
                    operation.OperationId),
                cancellationToken);

            if (recovery.Outcome == ProviderOperationOutcome.Rejected)
            {
                await ResolveUnreleasedReservationsAsync(order.Id, ProviderOperationOutcome.Rejected, cancellationToken);

                return await RejectAsync(order, operation, cancellationToken);
            }

            if (recovery.Outcome != ProviderOperationOutcome.Confirmed)
                return await ReconcileAsync(order, operation, recovery.Outcome, cancellationToken);

            var decision = WithdrawEligibilityPolicy.Evaluate(
                order,
                await DocumentedServiceIdsAsync(order.Id, cancellationToken));

            if (!decision.IsAllowed || !CanStillBeCancelled(order))
                return await ReconcileAsync(order, operation, ProviderOperationOutcome.Unknown, cancellationToken);

            await ResolveUnreleasedReservationsAsync(order.Id, ProviderOperationOutcome.Confirmed, cancellationToken);

            if (!await AllReservationsReleasedAsync(order.Id, cancellationToken))
                return await ReconcileAsync(order, operation, ProviderOperationOutcome.Unknown, cancellationToken);

            return await FinalizeAsync(
                order,
                operation,
                decision.EffectiveScopeServiceIds,
                reason,
                cancelledBy,
                cancellationToken);
        }

        private async Task<CancelOrderOutcome> ReconcileAsync(
            Order order,
            OrderOperation operation,
            ProviderOperationOutcome outcome,
            CancellationToken cancellationToken)
        {
            await _operationStore.TransitionAsync(
                operation.OperationId,
                ServicingOperationStatus.NeedsReconciliation,
                operation.ClaimGeneration,
                cancellationToken);

            await _receipts.SetStatusAsync(
                operation.ReceiptId,
                CommandReceiptStatus.NeedsReconciliation,
                cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(
                order,
                operation,
                [],
                outcome,
                ServicingOperationStatus.NeedsReconciliation,
                isReplay: true);
        }

        private async Task ResolveUnreleasedReservationsAsync(
            long orderId,
            ProviderOperationOutcome outcome,
            CancellationToken cancellationToken)
        {
            var reservations = await _reservations.ListByOrderAsync(orderId, cancellationToken);

            foreach (var reservation in reservations.Where(candidate =>
                         candidate.Status == FulfillmentReservationStatus.CancellationPending))
                ApplyReleaseOutcome(reservation, outcome);
        }

        private async Task<bool> AllReservationsReleasedAsync(long orderId, CancellationToken cancellationToken)
        {
            var reservations = await _reservations.ListByOrderAsync(orderId, cancellationToken);

            return reservations.All(reservation => reservation.Status == FulfillmentReservationStatus.Released);
        }

        private static bool CanStillBeCancelled(Order order)
        {
            try
            {
                order.EnsureCanBeCancelled();
                return true;
            }
            catch (Framework.Core.Domain.Exceptions.BusinessException)
            {
                return false;
            }
        }

        private async Task<IReadOnlyList<long>> DocumentedServiceIdsAsync(long orderId, CancellationToken cancellationToken)
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

            return documented.Distinct().ToList();
        }

        private async Task<ProviderOperationOutcome> ReleaseReservationsAsync(
            long orderId,
            OrderOperation operation,
            CancellationToken cancellationToken)
        {
            var reservations = await _reservations.ListByOrderAsync(orderId, cancellationToken);
            var observed = new List<ProviderOperationOutcome>();

            foreach (var reservation in reservations.Where(IsReleasable))
            {
                var result = await _reservationPort.ReleaseAsync(
                    new ReleaseReservationRequest(
                        _operations.ProviderOperationKey(operation, ReleaseStep),
                        orderId,
                        operation.OperationId,
                        reservation.ExternalReservationRef,
                        reservation.Services.Select(service => service.OrderServiceId).ToList()),
                    cancellationToken);

                ApplyReleaseOutcome(reservation, result.Outcome);
                observed.Add(result.Outcome);
            }

            return AggregateOutcome(observed);
        }

        private void ApplyReleaseOutcome(FulfillmentReservation reservation, ProviderOperationOutcome outcome)
        {
            switch (outcome)
            {
                case ProviderOperationOutcome.Confirmed:
                    reservation.MarkReleased(_clock);
                    break;

                case ProviderOperationOutcome.Rejected:
                    reservation.RestoreAfterUnreleasedCancellation(_clock);
                    break;

                default:
                    reservation.MarkCancellationPending(_clock);
                    break;
            }
        }

        private static ProviderOperationOutcome AggregateOutcome(IReadOnlyList<ProviderOperationOutcome> observed)
        {
            if (observed.Count == 0)
                return ProviderOperationOutcome.Confirmed;

            var unresolved = observed
                .Where(outcome => outcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown)
                .ToList();

            if (unresolved.Count > 0)
                return unresolved.Contains(ProviderOperationOutcome.Unknown)
                    ? ProviderOperationOutcome.Unknown
                    : ProviderOperationOutcome.Pending;

            return observed.Contains(ProviderOperationOutcome.Rejected)
                ? ProviderOperationOutcome.Rejected
                : ProviderOperationOutcome.Confirmed;
        }

        private static bool IsReleasable(FulfillmentReservation reservation)
            => reservation.Status is not (FulfillmentReservationStatus.Released or FulfillmentReservationStatus.Rejected);

        private static Entities.OrderChange? CommittedCancel(Order order, long operationId)
            => order.Changes.FirstOrDefault(change =>
                change.OperationId == operationId && change.ChangeType == OrderChangeType.Cancel);

        private async Task<CancelOrderOutcome> ReplayFinalizedAsync(
            Order order,
            OrderOperation operation,
            CancellationToken cancellationToken)
        {
            await _operations.ResolveAsync(order.Id, operation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var scope = order.OrderServices
                .Where(service => service.Status == OrderServiceStatus.Cancelled)
                .Select(service => service.Id)
                .ToList();

            return Outcome(
                order,
                operation,
                scope,
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

        private static CancelOrderOutcome Outcome(
            Order order,
            OrderOperation operation,
            IReadOnlyList<long> scope,
            ProviderOperationOutcome releaseOutcome,
            ServicingOperationStatus operationStatus,
            bool isReplay)
            => new(
                order.Id,
                operation.OperationId,
                order.Status,
                order.CommercialSummary,
                order.CommercialVersion,
                order.FinancialSequence,
                scope,
                releaseOutcome,
                operationStatus,
                isReplay);
    }
}
