using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Providers.Deterministic
{
    internal sealed record DeterministicDocumentRefundOperation(
        string Intent,
        ProviderOperationOutcome Outcome,
        string? ProviderReference,
        string DocumentNumber,
        IReadOnlyList<int> CouponNumbers)
    {
        public DeterministicDocumentRefundOperation Resolved(ProviderOperationOutcome recoveryOutcome)
            => Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected
                ? this
                : this with
                {
                    Outcome = recoveryOutcome,
                    ProviderReference = recoveryOutcome == ProviderOperationOutcome.Rejected
                        ? null
                        : ProviderReference
                };
    }
}
