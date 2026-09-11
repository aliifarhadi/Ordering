using AeroTech.Messages.Ordering.Enums;

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
        ProviderOperationOutcome? AssociationOutcome = null,
        string? AssociationProviderReference = null,
        string? AssociationDetail = null)
    {
        public const string LegPrefix = "emd-reassociate";

        public string LegIdentity => $"{LegPrefix}:{EmdDocumentNumber}:{EmdCouponNumber}";

        public bool IsReassociation => Disposition == AncillaryExchangeDisposition.ReassociateExisting;

        public bool IsSettled => AssociationOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsRejected => AssociationOutcome == ProviderOperationOutcome.Rejected;

        public bool IsUnresolved
            => AssociationOutcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown;

        public ExchangeAncillaryState State => AssociationOutcome switch
        {
            ProviderOperationOutcome.Confirmed => ExchangeAncillaryState.Confirmed,
            ProviderOperationOutcome.Rejected => ExchangeAncillaryState.Rejected,
            ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown => ExchangeAncillaryState.Pending,
            _ => IsReassociation ? ExchangeAncillaryState.NotStarted : ExchangeAncillaryState.NotRequired
        };
    }
}
