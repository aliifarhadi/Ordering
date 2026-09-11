using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ExchangeFunding;

namespace AeroTech.Ordering.Providers.Deterministic
{
    internal sealed record DeterministicFundingOperation(
        string Intent,
        ProviderOperationOutcome Outcome,
        string? ProviderReference,
        decimal? Amount,
        int? CurrencyId)
    {
        public ExchangeFundingResult AsResult() => new(Outcome, ProviderReference, Amount, CurrencyId);

        public DeterministicFundingOperation Resolved(ProviderOperationOutcome recoveryOutcome)
        {
            if (Outcome is ProviderOperationOutcome.Confirmed or ProviderOperationOutcome.Rejected)
                return this;

            return this with
            {
                Outcome = recoveryOutcome,
                ProviderReference = recoveryOutcome == ProviderOperationOutcome.Rejected ? null : ProviderReference
            };
        }
    }
}
