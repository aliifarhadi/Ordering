using AeroTech.Framework.Core.Domain.Repository;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Application.OrderAggregate.Operations;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Contracts;
using AeroTech.Ordering.Domain.OrderAggregate.Entities;
using AeroTech.Ordering.Domain.OrderAggregate.Policies;
using AeroTech.Ordering.Domain.Ports.Reservation;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Application.OrderAggregate.Services.Reservation
{
    public sealed record ReserveOrderOutcome(
        long OrderId,
        long OperationId,
        FulfillmentReservationStatus ReservationStatus,
        CommercialSummary CommercialSummary,
        int CommercialVersion,
        IReadOnlyList<long> ConfirmedServiceIds,
        IReadOnlyList<long> UnconfirmedServiceIds,
        string? Detail);

    public interface IReserveOrderService
    {
        Task<ReserveOrderOutcome> ReserveAsync(
            long orderId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default);
    }

    public sealed class ReserveOrderService : IReserveOrderService
    {
        public const string ProviderStep = "reserve";

        private readonly IOrderRepository _orders;
        private readonly IFulfillmentReservationRepository _reservations;
        private readonly IReservationPort _reservationPort;
        private readonly IOrderOperationCoordinator _operations;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IIdGenerator _idGenerator;
        private readonly IClock _clock;
        private readonly IOrderProjector _projector;

        public ReserveOrderService(
            IOrderRepository orders,
            IFulfillmentReservationRepository reservations,
            IReservationPort reservationPort,
            IOrderOperationCoordinator operations,
            IUnitOfWork unitOfWork,
            IIdGenerator idGenerator,
            IClock clock,
            IOrderProjector projector)
        {
            _orders = orders;
            _reservations = reservations;
            _reservationPort = reservationPort;
            _operations = operations;
            _unitOfWork = unitOfWork;
            _idGenerator = idGenerator;
            _clock = clock;
            _projector = projector;
        }

        public async Task<ReserveOrderOutcome> ReserveAsync(
            long orderId,
            string idempotencyKey,
            int? expectedCommercialVersion,
            CancellationToken cancellationToken = default)
        {
            var order = await _orders.GetAsync(orderId, cancellationToken)
                        ?? throw ExceptionFactory.OrderNotFound(orderId);

            EnsureExpectedVersion(order, expectedCommercialVersion);

            var existing = await _reservations.ListByOrderAsync(orderId, cancellationToken);
            var alreadyReserved = existing
                .SelectMany(reservation => reservation.Services)
                .Where(service => service.ObservedStatus != ReservationMemberStatus.Released)
                .Select(service => service.OrderServiceId)
                .ToList();

            var decision = ReserveEligibilityPolicy.Evaluate(order, alreadyReserved);

            if (!decision.IsAllowed)
                throw ExceptionFactory.OrderOperationNotEligible(ServicingOperationKind.Reserve, orderId, decision.Reasons);

            var scope = decision.EffectiveScopeServiceIds;

            var operation = await _operations.BeginAsync(
                orderId,
                ServicingOperationKind.Reserve,
                idempotencyKey,
                new { Operation = "Reserve", OrderId = orderId, Services = scope.OrderBy(id => id).ToArray() },
                expectedCommercialVersion,
                cancellationToken);

            var reservation = await _reservations.GetByOperationAsync(operation.OperationId, cancellationToken);

            if (reservation is null)
            {
                reservation = FulfillmentReservation.Open(
                    _idGenerator.NewId(),
                    orderId,
                    operation.OperationId,
                    OrderProviderType.Airline,
                    null,
                    scope,
                    _idGenerator,
                    _clock);

                await _reservations.AddAsync(reservation, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            var request = new ReserveRequest(
                _operations.ProviderOperationKey(operation, ProviderStep),
                orderId,
                operation.OperationId,
                order.TimeToLive,
                BuildServiceRequests(order, scope));

            var result = await _reservationPort.ReserveAsync(request, cancellationToken);

            reservation.Observe(
                result.Services
                    .Select(service => new ReservationServiceObservation(
                        service.OrderServiceId,
                        service.Status,
                        service.ExternalServiceRef,
                        service.ObservedBookingClass,
                        service.ExternalStatus,
                        service.ValidUntil))
                    .ToList(),
                result.ExternalReservationRef,
                result.ExpiresAt,
                _clock);

            var confirmed = result.Services
                .Where(service => service.Status == ReservationMemberStatus.Confirmed)
                .Select(service => service.OrderServiceId)
                .ToList();

            order.ApplyReservationOutcome(confirmed, result.ExternalReservationRef, result.ExpiresAt, _idGenerator, _clock);

            if (result.Outcome != ProviderOperationOutcome.Unknown)
                await _operations.ResolveAsync(orderId, operation, cancellationToken);

            await _projector.ProjectAsync(orderId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ReserveOrderOutcome(
                orderId,
                operation.OperationId,
                reservation.Status,
                order.CommercialSummary,
                order.CommercialVersion,
                confirmed,
                reservation.UnconfirmedServiceIds().ToList(),
                result.Detail);
        }

        private static IReadOnlyList<ReserveServiceRequest> BuildServiceRequests(
            Domain.OrderAggregate.Order order,
            IReadOnlyList<long> scope)
            => order.OrderServices
                .Where(service => service.IsAirTransport && scope.Contains(service.Id))
                .Select(service =>
                {
                    var segment = order.Segments.Single(candidate => candidate.Id == service.SoldSegmentId!.Value);

                    return new ReserveServiceRequest(
                        service.Id,
                        service.SoleBeneficiaryId,
                        segment.Id,
                        segment.FlightCapacityId,
                        segment.BookingClass);
                })
                .ToList();

        private static void EnsureExpectedVersion(Domain.OrderAggregate.Order order, int? expectedCommercialVersion)
        {
            if (expectedCommercialVersion is { } expected && expected != order.CommercialVersion)
                throw ExceptionFactory.OrderCommercialVersionMismatch(expected, order.Id, order.CommercialVersion);
        }
    }
}
