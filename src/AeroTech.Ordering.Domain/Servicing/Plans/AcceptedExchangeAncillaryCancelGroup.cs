using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Domain.Servicing.Plans
{
    public sealed record AcceptedExchangeAncillaryCancelGroup(
        string CancelGroupRef,
        long ElectronicMiscDocumentId,
        string EmdDocumentNumber,
        IReadOnlyList<int> EmdCouponNumbers,
        IReadOnlyList<long> OrderServiceIds,
        string CancellationReference,
        string SourceReference,
        AncillaryCancellationDocumentAction DocumentAction,
        string DecisionReference,
        EligibilityOutcome? VoidEligibilityOutcome = null,
        bool VoidRefundRequiredInstead = false,
        string? VoidEligibilityDetail = null,
        DateTimeOffset? VoidDispatchedAt = null,
        ProviderOperationOutcome? VoidOutcome = null,
        string? VoidProviderReference = null,
        string? VoidDetail = null,
        DateTimeOffset? CancellationSettledAt = null)
    {
        public const string LegPrefix = "emd-cancel";

        public static string RefOf(string emdDocumentNumber) => $"{LegPrefix}:{emdDocumentNumber}";

        public string LegIdentity => CancelGroupRef;

        public bool IsEligibilityAllowed => VoidEligibilityOutcome == EligibilityOutcome.Allowed;

        public bool IsEligibilityDenied => VoidEligibilityOutcome == EligibilityOutcome.Denied;

        public bool RequiresRefundDecisionInstead => VoidRefundRequiredInstead;

        public bool WasVoidDispatched => VoidDispatchedAt is not null;

        public bool IsVoidConfirmed => VoidOutcome == ProviderOperationOutcome.Confirmed;

        public bool IsVoidRejected => VoidOutcome == ProviderOperationOutcome.Rejected;

        public bool IsVoidUnresolved
            => VoidOutcome is ProviderOperationOutcome.Pending or ProviderOperationOutcome.Unknown;

        public bool IsSettled => CancellationSettledAt is not null;

        public bool IsRejected => IsVoidRejected || IsEligibilityDenied || RequiresRefundDecisionInstead;

        public ExchangeAncillaryState State => IsRejected
            ? ExchangeAncillaryState.Rejected
            : IsSettled
                ? ExchangeAncillaryState.Confirmed
                : VoidEligibilityOutcome is null && VoidOutcome is null
                    ? ExchangeAncillaryState.NotStarted
                    : ExchangeAncillaryState.Pending;
    }
}
