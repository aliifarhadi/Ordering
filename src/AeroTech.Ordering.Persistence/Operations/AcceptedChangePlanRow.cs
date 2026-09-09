using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Persistence.Operations
{
    public sealed class AcceptedChangePlanRow
    {
        public long OperationId { get; set; }

        public long OrderId { get; set; }

        public string QuotedChangeId { get; set; } = null!;

        public string SourceSystem { get; set; } = null!;

        public string TargetSelectionRef { get; set; } = null!;

        public long ElectronicTicketId { get; set; }

        public long ReplacedOrderServiceId { get; set; }

        public long ReplacementOrderServiceId { get; set; }

        public long ReplacementOrderSegmentId { get; set; }

        public long TicketCouponId { get; set; }

        public int ExpectedCommercialVersion { get; set; }

        public ChangeMonetaryOutcome MonetaryOutcome { get; set; }

        public string AcceptedPlan { get; set; } = null!;

        public DocumentChangeEligibilityOutcome? EligibilityOutcome { get; set; }

        public string? EligibilityDetail { get; set; }

        public ProviderOperationOutcome ReservationOutcome { get; set; }

        public string? ReservationExternalRef { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
