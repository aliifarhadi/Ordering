using AeroTech.Framework.Core.Domain.Entities;
using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.FulfillmentReservationAggregate.Entities
{
    public sealed class FulfillmentReservationService : Entity<long>
    {
        private FulfillmentReservationService()
        {
        }

        internal FulfillmentReservationService(long id, long fulfillmentReservationId, long orderServiceId)
        {
            Id = id;
            FulfillmentReservationId = fulfillmentReservationId;
            OrderServiceId = orderServiceId;
            ObservedStatus = ReservationMemberStatus.Pending;
        }

        public long FulfillmentReservationId { get; private set; }

        public long OrderServiceId { get; private set; }

        public string? ExternalServiceRef { get; private set; }

        public ReservationMemberStatus ObservedStatus { get; private set; }

        public string? ObservedBookingClass { get; private set; }

        public string? ExternalStatus { get; private set; }

        public DateTimeOffset? ValidUntil { get; private set; }

        internal void Observe(
            ReservationMemberStatus status,
            string? externalServiceRef,
            string? observedBookingClass,
            string? externalStatus,
            DateTimeOffset? validUntil)
        {
            ObservedStatus = status;
            ExternalServiceRef = externalServiceRef ?? ExternalServiceRef;
            ObservedBookingClass = observedBookingClass ?? ObservedBookingClass;
            ExternalStatus = externalStatus ?? ExternalStatus;
            ValidUntil = validUntil ?? ValidUntil;
        }

        internal void Release() => ObservedStatus = ReservationMemberStatus.Released;
    }
}
