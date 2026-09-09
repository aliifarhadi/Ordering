using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicMiscDocumentAggregate.Contracts;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Ports.Reservation;
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

            var committed = CommittedCancel(order, operation.OperationId);

            if (committed is not null)
                return await ReplayAsync(order, operation, cancellationToken);

            IReadOnlyList<long> scope;
            var releaseOutcome = ProviderOperationOutcome.Confirmed;

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
                releaseOutcome = await ReleaseReservationsAsync(orderId, operation, cancellationToken);

                order.Cancel(reason, cancelledBy, _clock.GetDateTime(), _idGenerator, operation.OperationId);
                order.ApplyReservationReleased(scope, _clock);
            }
            catch
            {
                await TryReleaseRejectedAsync(orderId, operation, cancellationToken);
                throw;
            }

            if (releaseOutcome != ProviderOperationOutcome.Unknown)
                await _operations.ResolveAsync(orderId, operation, cancellationToken);

            await _projector.ProjectAsync(orderId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Outcome(order, operation, scope, releaseOutcome, isReplay: false);
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
            var outcome = ProviderOperationOutcome.Confirmed;

            foreach (var reservation in reservations.Where(candidate =>
                         candidate.Status is not (FulfillmentReservationStatus.Released or FulfillmentReservationStatus.Rejected)))
            {
                reservation.MarkCancellationPending(_clock);

                var result = await _reservationPort.ReleaseAsync(
                    new ReleaseReservationRequest(
                        _operations.ProviderOperationKey(operation, ReleaseStep),
                        orderId,
                        operation.OperationId,
                        reservation.ExternalReservationRef,
                        reservation.Services.Select(service => service.OrderServiceId).ToList()),
                    cancellationToken);

                if (result.Outcome == ProviderOperationOutcome.Confirmed)
                    reservation.MarkReleased(_clock);
                else
                    outcome = result.Outcome;
            }

            return outcome;
        }

        private static Entities.OrderChange? CommittedCancel(Order order, long operationId)
            => order.Changes.FirstOrDefault(change =>
                change.OperationId == operationId && change.ChangeType == OrderChangeType.Cancel);

        private async Task<CancelOrderOutcome> ReplayAsync(
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

            return Outcome(order, operation, scope, ProviderOperationOutcome.Confirmed, isReplay: true);
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
                isReplay);
    }
}
