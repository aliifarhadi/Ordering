using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Query.OrderAggregate.Models
{
    public sealed class ServicingOperationReadModel
    {
        public long Id { get; set; }

        public long OrderId { get; set; }

        public long? CommandReceiptId { get; set; }

        public ServicingOperationKind Kind { get; set; }

        public ServicingOperationStatus Status { get; set; }

        public int? ExpectedCommercialVersion { get; set; }

        public long ClaimGeneration { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
