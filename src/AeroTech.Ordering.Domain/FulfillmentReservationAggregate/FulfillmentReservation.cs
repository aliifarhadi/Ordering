using AeroTech.Framework.Core.Domain.Aggregates;
using AeroTech.Framework.Core.ServiceContracts;
using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Entities;
using AeroTech.Ordering.Domain._Shared.Resources;

namespace AeroTech.Ordering.Domain.FulfillmentReservationAggregate
{
    public sealed class FulfillmentReservation : AggregateRoot<long>
    {
        private readonly List<FulfillmentReservationService> _services = new();

        private FulfillmentReservation()
        {
        }

        private FulfillmentReservation(
            long id,
            long orderId,
            long operationId,
            OrderProviderType providerType,
            string? providerCode,
            DateTimeOffset createdAt)
        {
            Id = id;
            OrderId = orderId;
            OperationId = operationId;
            ProviderType = providerType;
            ProviderCode = providerCode;
            Status = FulfillmentReservationStatus.Pending;
            LastUpdatedAt = createdAt;
        }

        public long OrderId { get; private set; }

        public long OperationId { get; private set; }

        public OrderProviderType ProviderType { get; private set; }

        public string? ProviderCode { get; private set; }

        public string? ExternalReservationRef { get; private set; }

        public FulfillmentReservationStatus Status { get; private set; }

        public DateTimeOffset? ExpiresAt { get; private set; }

        public DateTimeOffset LastUpdatedAt { get; private set; }

        public IReadOnlyCollection<FulfillmentReservationService> Services => _services.AsReadOnly();

        public static FulfillmentReservation Open(
            long id,
            long orderId,
            long operationId,
            OrderProviderType providerType,
            string? providerCode,
            IReadOnlyCollection<long> orderServiceIds,
            IIdGenerator idGenerator,
            IClock clock)
        {
            if (orderServiceIds.Count == 0)
                throw ExceptionFactory.ReservationRequiresAtLeastOneService();

            var reservation = new FulfillmentReservation(id, orderId, operationId, providerType, providerCode, clock.GetDateTime());

            foreach (var orderServiceId in orderServiceIds.Distinct())
                reservation._services.Add(new FulfillmentReservationService(idGenerator.NewId(), id, orderServiceId));

            return reservation;
        }

        public void Observe(
            IReadOnlyCollection<ReservationServiceObservation> observations,
            string? externalReservationRef,
            DateTimeOffset? expiresAt,
            IClock clock)
        {
            foreach (var observation in observations)
            {
                var member = _services.SingleOrDefault(service => service.OrderServiceId == observation.OrderServiceId)
                             ?? throw ExceptionFactory.ReservationServiceNotFound(observation.OrderServiceId, Id);

                member.Observe(
                    observation.Status,
                    observation.ExternalServiceRef,
                    observation.ObservedBookingClass,
                    observation.ExternalStatus,
                    observation.ValidUntil);
            }

            ExternalReservationRef = externalReservationRef ?? ExternalReservationRef;
            ExpiresAt = expiresAt ?? ExpiresAt;
            LastUpdatedAt = clock.GetDateTime();

            RecomputeStatus();
        }

        public void MarkCancellationPending(IClock clock)
        {
            Status = FulfillmentReservationStatus.CancellationPending;
            LastUpdatedAt = clock.GetDateTime();
        }

        public void RestoreAfterUnreleasedCancellation(IClock clock)
        {
            if (Status != FulfillmentReservationStatus.CancellationPending)
                return;

            RecomputeStatus();
            LastUpdatedAt = clock.GetDateTime();
        }

        public void MarkReleased(IClock clock)
        {
            foreach (var member in _services)
                member.Release();

            Status = FulfillmentReservationStatus.Released;
            LastUpdatedAt = clock.GetDateTime();
        }

        public bool IsConfirmedFor(long orderServiceId)
            => _services.Any(service =>
                service.OrderServiceId == orderServiceId
                && service.ObservedStatus == ReservationMemberStatus.Confirmed);

        public IReadOnlyCollection<long> UnconfirmedServiceIds()
            => _services
                .Where(service => service.ObservedStatus != ReservationMemberStatus.Confirmed)
                .Select(service => service.OrderServiceId)
                .ToList();

        private void RecomputeStatus()
        {
            var statuses = _services.Select(service => service.ObservedStatus).Distinct().ToList();

            if (statuses.Count == 1)
            {
                Status = statuses[0] switch
                {
                    ReservationMemberStatus.Pending => FulfillmentReservationStatus.Pending,
                    ReservationMemberStatus.Waitlisted => FulfillmentReservationStatus.Waitlisted,
                    ReservationMemberStatus.Confirmed => FulfillmentReservationStatus.Confirmed,
                    ReservationMemberStatus.Rejected => FulfillmentReservationStatus.Rejected,
                    ReservationMemberStatus.Released => FulfillmentReservationStatus.Released,
                    ReservationMemberStatus.Expired => FulfillmentReservationStatus.Expired,
                    _ => FulfillmentReservationStatus.Unknown
                };

                return;
            }

            Status = FulfillmentReservationStatus.Mixed;
        }
    }

    public sealed record ReservationServiceObservation(
        long OrderServiceId,
        ReservationMemberStatus Status,
        string? ExternalServiceRef = null,
        string? ObservedBookingClass = null,
        string? ExternalStatus = null,
        DateTimeOffset? ValidUntil = null);
}
