using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class FulfillmentReservationReadModel
    {
        public long Id { get; set; }

        public long OrderId { get; set; }

        public long OperationId { get; set; }

        public string? ExternalReservationRef { get; set; }

        public FulfillmentReservationStatus Status { get; set; }
    }
}
