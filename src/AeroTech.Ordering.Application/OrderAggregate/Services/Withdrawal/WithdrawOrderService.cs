using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.ElectronicTicketAggregate.Contracts;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Ports.Funding;
using AeroTech.Ordering.Domain.Ports.Reservation;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Withdrawal
{
    public sealed class WithdrawOrderService : IWithdrawOrderService
    {
        public const string ReleaseStep = "release";
        public const string FundingReleaseStep = "coverage-release";

        private readonly IOrderRepository _orders;
        private readonly IFulfillmentReservationRepository _reservations;
        private readonly IElectronicTicketRepository _tickets;
        private readonly IReservationPort _reservationPort;
        private readonly IFundingCoveragePort _funding;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IIdentityService _identity;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;

        public WithdrawOrderService(
            IOrderRepository orders,
            IFulfillmentReservationRepository reservations,
            IElectronicTicketRepository tickets,
            IReservationPort reservationPort,
            IFundingCoveragePort funding,
            IOrderOperationCoordinator operations,
            IIdentityService identity,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock,
            IOrderProjector projector)
        {
            _orders = orders;
            _reservations = reservations;
            _tickets = tickets;
            _reservationPort = reservationPort;
            _funding = funding;
            _operations = operations;
            _identity = identity;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
            _projector = projector;
        }

        public async Task<WithdrawOrderOutcome> WithdrawAsync(
            long orderId,
            VoidReason reason,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default)
        {
            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            if (expectedCommercialVersion is { } expected && expected != order.CommercialVersion)
                throw ExceptionFactory.OrderCommercialVersionMismatch(expected, orderId, order.CommercialVersion);

            var tickets = await _tickets.ListByOrderAsync(orderId, cancellationToken);

            var documented = tickets
                .SelectMany(ticket => ticket.Coupons)
                .Where(coupon => coupon.FinancialStatus != TicketCouponFinancialStatus.Void)
                .Select(coupon => coupon.CurrentOrderServiceId)
                .ToList();

            var decision = WithdrawEligibilityPolicy.Evaluate(order, documented);

            if (!decision.IsAllowed)
                throw ExceptionFactory.OrderOperationNotEligible(ServicingOperationKind.Cancel, orderId, decision.Reasons);

            var scope = decision.EffectiveScopeServiceIds;

            var operation = await _operations.BeginAsync(
                orderId,
                ServicingOperationKind.Cancel,
                idempotencyKey,
                new { Operation = "Withdraw", OrderId = orderId, Reason = reason.ToString() },
                expectedCommercialVersion,
                cancellationToken);

            var reservations = await _reservations.ListByOrderAsync(orderId, cancellationToken);
            var releaseOutcome = ProviderOperationOutcome.Confirmed;

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
                    releaseOutcome = result.Outcome;
            }

            order.ApplyReservationReleased(scope, _clock);

            var fundingRelease = await _funding.RequestReleaseAsync(
                new FundingReleaseRequest(
                    _operations.ProviderOperationKey(operation, FundingReleaseStep),
                    orderId,
                    operation.OperationId,
                    null),
                cancellationToken);

            order.WithdrawBeforeTicketing(scope, reason, _identity.CurrentUserId ?? 0, _idGenerator, _clock);

            if (releaseOutcome != ProviderOperationOutcome.Unknown)
                await _operations.ResolveAsync(orderId, operation, cancellationToken);

            await _projector.ProjectAsync(orderId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new WithdrawOrderOutcome(
                orderId,
                operation.OperationId,
                order.CommercialSummary,
                order.CommercialVersion,
                scope,
                releaseOutcome,
                fundingRelease.Outcome);
        }
    }
}
