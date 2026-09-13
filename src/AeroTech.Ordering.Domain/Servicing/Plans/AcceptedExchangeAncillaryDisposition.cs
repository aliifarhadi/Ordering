using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.OrderAggregate.AcceptedSource.Refund;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record AcceptedExchangeAncillaryDisposition(
        long ElectronicMiscDocumentId,
        string EmdDocumentNumber,
        int EmdCouponNumber,
        long EmdCouponId,
        long PredecessorTicketCouponId,
        string PredecessorDocumentNumber,
        int PredecessorCouponNumber,
        AncillaryExchangeDisposition Disposition,
        int? TargetPredecessorCouponNumber,
        long? TargetSuccessorTicketCouponId,
        string DecisionReference,
        int DecisionVersion,
        string DecisionContextFingerprint,
        decimal? RefundAmount = null,
        int? RefundCurrencyId = null,
        string? RefundDisposition = null,
        string? RefundSourceReference = null,
        PricingSource? RefundPricingSource = null,
        IReadOnlyList<AcceptedRefundPricingLine>? RefundPricingLines = null,
        long? RefundPriceChangeSetId = null,
        string? ExchangeGroupRef = null,
        string? RetentionReference = null,
        string? RetentionSourceReference = null,
        AncillaryRetentionMode? RetentionMode = null,
        DateTimeOffset? RetentionSettledAt = null,
        string? CancelGroupRef = null,
        string? CancellationReference = null,
        string? CancellationSourceReference = null,
        AncillaryCancellationDocumentAction? CancellationDocumentAction = null,
        long? CancelledOrderServiceId = null,
        string? ManualReviewReason = null,
        long? RefundedOrderServiceId = null,
        ProviderOperationOutcome? AssociationOutcome = null,
        string? AssociationProviderReference = null,
        string? AssociationDetail = null,
        ProviderOperationOutcome? RefundDocumentOutcome = null,
        string? RefundDocumentReference = null,
        string? RefundDocumentDetail = null,
        ProviderOperationOutcome? RefundValueOutcome = null,
        string? RefundValueReference = null,
        string? RefundValueDetail = null)
    {
        public const string LegPrefix = "emd-reassociate";

        public string LegIdentity => $"{LegPrefix}:{EmdDocumentNumber}:{EmdCouponNumber}";

        public const string RefundLegPrefix = "emd-refund";

        public const string RefundValueLegPrefix = "emd-refund-value";

        public bool IsReassociation => Disposition == AncillaryExchangeDisposition.ReassociateExisting;

        public bool IsRefund => Disposition == AncillaryExchangeDisposition.Refund;

        public bool IsEmdExchange => Disposition == AncillaryExchangeDisposition.ExchangeToNewEmd;

        public bool IsRetention => Disposition == AncillaryExchangeDisposition.RetainAsResidual;

        public bool IsCancel => Disposition == AncillaryExchangeDisposition.Cancel;

        public bool IsManualReview => Disposition == AncillaryExchangeDisposition.ManualReview;

        public bool IsRetentionSettled => RetentionSettledAt is not null;

        public string RefundLegIdentity => $"{RefundLegPrefix}:{EmdDocumentNumber}:{EmdCouponNumber}";

        public string RefundValueLegIdentity => $"{RefundValueLegPrefix}:{EmdDocumentNumber}:{EmdCouponNumber}";

        public bool IsRefundDocumentSettled => RefundDocumentOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsRefundDocumentRejected => RefundDocumentOutcome == ProviderOperationOutcome.Rejected;

        public bool IsRefundValueSettled => RefundValueOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsRefundValueRejected => RefundValueOutcome == ProviderOperationOutcome.Rejected;

        public bool IsRefundSettled => IsRefundDocumentSettled && IsRefundValueSettled;

        public bool IsRefundConsequenceCommitted => RefundPriceChangeSetId is not null;

        public ExchangeAncillaryState RefundState => RefundDocumentOutcome switch
        {
            null => ExchangeAncillaryState.NotStarted,
            ProviderOperationOutcome.Rejected => ExchangeAncillaryState.Rejected,
            ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown
                => ExchangeAncillaryState.Pending,
            _ => RefundValueOutcome switch
            {
                null => ExchangeAncillaryState.Pending,
                ProviderOperationOutcome.Confirmed => ExchangeAncillaryState.Confirmed,
                ProviderOperationOutcome.Rejected => ExchangeAncillaryState.Rejected,
                _ => ExchangeAncillaryState.Pending
            }
        };

        public bool IsSettled => Disposition switch
        {
            AncillaryExchangeDisposition.Refund => IsRefundSettled,
            AncillaryExchangeDisposition.RetainAsResidual => IsRetentionSettled,
            _ => AssociationOutcome == ProviderOperationOutcome.Confirmed
        };

        public bool IsRejected => Disposition switch
        {
            AncillaryExchangeDisposition.Refund => IsRefundDocumentRejected || IsRefundValueRejected,
            AncillaryExchangeDisposition.RetainAsResidual => false,
            _ => AssociationOutcome == ProviderOperationOutcome.Rejected
        };

        public bool IsUnresolved
            => AssociationOutcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown;

        public ExchangeAncillaryState State => Disposition switch
        {
            AncillaryExchangeDisposition.Refund => RefundState,
            AncillaryExchangeDisposition.RetainAsResidual => RetentionState,
            _ => AssociationState
        };

        private ExchangeAncillaryState RetentionState => IsRetentionSettled
            ? ExchangeAncillaryState.Confirmed
            : ExchangeAncillaryState.NotStarted;

        private ExchangeAncillaryState AssociationState => AssociationOutcome switch
        {
            ProviderOperationOutcome.Confirmed => ExchangeAncillaryState.Confirmed,
            ProviderOperationOutcome.Rejected => ExchangeAncillaryState.Rejected,
            ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown => ExchangeAncillaryState.Pending,
            _ => IsReassociation ? ExchangeAncillaryState.NotStarted : ExchangeAncillaryState.NotRequired
        };
    }
}
