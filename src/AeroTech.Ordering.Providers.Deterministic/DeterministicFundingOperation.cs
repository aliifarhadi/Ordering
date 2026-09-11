using AeroTech.Messages.Ordering.Enums;
using AeroTech.Ordering.Domain.Ports.ExchangeFunding;

namespace AeroTech.Ordering.Providers.Deterministic
{
    internal sealed record DeterministicFundingOperation(
        ProviderOperationOutcome Outcome,
        string? ProviderReference,
        decimal? Amount,
        int? CurrencyId)
    {
        public ExchangeFundingResult AsResult() => new(Outcome, ProviderReference, Amount, CurrencyId);
    }
}
