using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Persistence.Operations
{
    public sealed class ServicingOperation
    {
        public long Id { get; set; }

        public long OwnerAirlineId { get; set; }

        public long OrderId { get; set; }

        public long? CommandReceiptId { get; set; }

        public ServicingOperationKind Kind { get; set; }

        public ServicingOperationStatus Status { get; set; }

        public string RequestHash { get; set; } = null!;

        public int? ExpectedCommercialVersion { get; set; }

        public string? QuoteRef { get; set; }

        public long ClaimGeneration { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
