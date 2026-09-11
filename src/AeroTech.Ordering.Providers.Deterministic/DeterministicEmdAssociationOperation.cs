using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Providers.Deterministic
{
    internal sealed record DeterministicEmdAssociationOperation(
        string Intent,
        ProviderOperationOutcome Outcome,
        string? ProviderReference,
        string EmdDocumentNumber,
        int EmdCouponNumber,
        string AssociatedDocumentNumber,
        int AssociatedCouponNumber)
    {
        public DeterministicEmdAssociationOperation Resolved(ProviderOperationOutcome recoveryOutcome)
            => Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected
                ? this
                : this with
                {
                    Outcome = recoveryOutcome,
                    ProviderReference = recoveryOutcome == ProviderOperationOutcome.Rejected ? null : ProviderReference
                };
    }
}
