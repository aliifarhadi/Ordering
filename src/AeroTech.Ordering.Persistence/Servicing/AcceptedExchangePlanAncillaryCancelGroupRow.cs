using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanAncillaryCancelGroupRow
    {
        public long OperationId { get; set; }

        public string CancelGroupRef { get; set; } = null!;

        public long ElectronicMiscDocumentId { get; set; }

        public string EmdDocumentNumber { get; set; } = null!;

        public string EmdCouponNumbers { get; set; } = null!;

        public string OrderServiceIds { get; set; } = null!;

        public string CancellationReference { get; set; } = null!;

        public string SourceReference { get; set; } = null!;

        public AncillaryCancellationDocumentAction DocumentAction { get; set; }

        public string DecisionReference { get; set; } = null!;

        public EligibilityOutcome? VoidEligibilityOutcome { get; set; }

        public bool VoidRefundRequiredInstead { get; set; }

        public string? VoidEligibilityDetail { get; set; }

        public DateTimeOffset? VoidDispatchedAt { get; set; }

        public ProviderOperationOutcome? VoidOutcome { get; set; }

        public string? VoidProviderReference { get; set; }

        public string? VoidDetail { get; set; }

        public DateTimeOffset? CancellationSettledAt { get; set; }
    }
}
