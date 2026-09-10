using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanRow
    {
        public long OperationId { get; set; }

        public long OrderId { get; set; }

        public string QuotedExchangeId { get; set; } = null!;

        public string SourceSystem { get; set; } = null!;

        public string TargetSelectionRef { get; set; } = null!;

        public string? SourcePricingReference { get; set; }

        public PricingSource PricingSource { get; set; }

        public int SaleCurrencyId { get; set; }

        public long PredecessorElectronicTicketId { get; set; }

        public string PredecessorDocumentNumber { get; set; } = null!;

        public long PredecessorTicketCouponId { get; set; }

        public int PredecessorCouponNumber { get; set; }

        public long PredecessorOrderServiceId { get; set; }

        public long ReplacementOrderServiceId { get; set; }

        public long ReplacementOrderSegmentId { get; set; }

        public long SuccessorElectronicTicketId { get; set; }

        public long SuccessorTicketCouponId { get; set; }

        public int ExpectedCommercialVersion { get; set; }

        public ChangeMonetaryOutcome MonetaryOutcome { get; set; }

        public string AcceptedPlan { get; set; } = null!;

        public AcceptedExchangeDisposition Disposition { get; set; }

        public string? DispositionDetail { get; set; }

        public int? RejectionCode { get; set; }

        public int? RejectionHttpStatus { get; set; }

        public DocumentExchangeEligibilityOutcome? EligibilityOutcome { get; set; }

        public string? EligibilityDetail { get; set; }

        public ProviderOperationOutcome ReservationOutcome { get; set; }

        public string? ReservationExternalRef { get; set; }

        public ProviderOperationOutcome? DocumentExchangeOutcome { get; set; }

        public string? DocumentExchangeProviderReference { get; set; }

        public string? DocumentExchangeDetail { get; set; }

        public string? SuccessorDocumentNumber { get; set; }

        public int? SuccessorCouponNumber { get; set; }

        public long? SuccessorIssuerCarrierId { get; set; }

        public long? SuccessorIssuingOfficeId { get; set; }

        public DocumentAuthority? SuccessorAuthority { get; set; }

        public DateTimeOffset? SuccessorVoidDeadline { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
