using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class FulfillmentReservationServiceReadModel
    {
        public long Id { get; set; }

        public long FulfillmentReservationId { get; set; }

        public long OrderServiceId { get; set; }

        public string? ExternalServiceRef { get; set; }

        public ReservationMemberStatus ObservedStatus { get; set; }

        public string? ExternalStatus { get; set; }
    }
}
