using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Persistence.Servicing
{
    public sealed class AcceptedExchangePlanAncillaryRow
    {
        public long OperationId { get; set; }

        public long ElectronicMiscDocumentId { get; set; }

        public string EmdDocumentNumber { get; set; } = null!;

        public int EmdCouponNumber { get; set; }

        public long EmdCouponId { get; set; }

        public long PredecessorTicketCouponId { get; set; }

        public string PredecessorDocumentNumber { get; set; } = null!;

        public int PredecessorCouponNumber { get; set; }

        public AncillaryExchangeDisposition Disposition { get; set; }

        public int? TargetPredecessorCouponNumber { get; set; }

        public long? TargetSuccessorTicketCouponId { get; set; }

        public string DecisionReference { get; set; } = null!;

        public int DecisionVersion { get; set; }

        public string DecisionContextFingerprint { get; set; } = null!;

        public decimal? RefundAmount { get; set; }

        public int? RefundCurrencyId { get; set; }

        public string? RefundDisposition { get; set; }

        public string? RefundSourceReference { get; set; }

        public PricingSource? RefundPricingSource { get; set; }

        public string? RefundPricingLines { get; set; }

        public long? RefundPriceChangeSetId { get; set; }

        public string? ExchangeGroupRef { get; set; }

        public string? RetentionReference { get; set; }

        public string? RetentionSourceReference { get; set; }

        public AncillaryRetentionMode? RetentionMode { get; set; }

        public DateTimeOffset? RetentionSettledAt { get; set; }

        public string? CancelGroupRef { get; set; }

        public string? CancellationReference { get; set; }

        public string? CancellationSourceReference { get; set; }

        public AncillaryCancellationDocumentAction? CancellationDocumentAction { get; set; }

        public long? CancelledOrderServiceId { get; set; }

        public string? ManualReviewReason { get; set; }

        public long? RefundedOrderServiceId { get; set; }

        public ProviderOperationOutcome? RefundDocumentOutcome { get; set; }

        public string? RefundDocumentReference { get; set; }

        public string? RefundDocumentDetail { get; set; }

        public ProviderOperationOutcome? RefundValueOutcome { get; set; }

        public string? RefundValueReference { get; set; }

        public string? RefundValueDetail { get; set; }

        public ProviderOperationOutcome? AssociationOutcome { get; set; }

        public string? AssociationProviderReference { get; set; }

        public string? AssociationDetail { get; set; }
    }
}
