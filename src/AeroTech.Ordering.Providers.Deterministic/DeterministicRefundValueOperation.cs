using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Providers.Deterministic
{
    internal sealed record DeterministicRefundValueOperation(
        string Intent,
        ProviderOperationOutcome Outcome,
        string? ValueMovementReference,
        decimal Amount,
        int CurrencyId,
        string Disposition)
    {
        public DeterministicRefundValueOperation Resolved(ProviderOperationOutcome recoveryOutcome)
            => Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected
                ? this
                : this with
                {
                    Outcome = recoveryOutcome,
                    ValueMovementReference = recoveryOutcome == ProviderOperationOutcome.Rejected
                        ? null
                        : ValueMovementReference
                };
    }
}
