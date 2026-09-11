using AeroTech.Messages.Ordering.Enums;

namespace AeroTech.Ordering.Providers.Deterministic
{
    internal sealed record DeterministicResidualOperation(
        string Intent,
        ProviderOperationOutcome Outcome,
        string? ProviderReference,
        string? InstrumentReference,
        ResidualInstrumentKind? Instrument,
        decimal Amount,
        int CurrencyId)
    {
        public DeterministicResidualOperation Resolved(ProviderOperationOutcome recoveryOutcome)
            => Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected
                ? this
                : this with
                {
                    Outcome = recoveryOutcome,
                    ProviderReference = recoveryOutcome == ProviderOperationOutcome.Rejected ? null : ProviderReference,
                    InstrumentReference = recoveryOutcome == ProviderOperationOutcome.Rejected
                        ? null
                        : InstrumentReference
                };
    }
}
